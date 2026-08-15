using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Snapshots;

/// <summary>
/// Fixed-root, atomic, bounded safety-snapshot storage. Integrity is checked before a snapshot can
/// authorize a future repair; malformed, tampered, stale, expired, or wrong-principal records fail closed.
/// </summary>
public sealed class FileSafetySnapshotStore(SafetySnapshotOptions options) : ISafetySnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task SaveAsync(SafetySnapshotDocument snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateId(snapshot.SnapshotId);
        if (string.IsNullOrWhiteSpace(snapshot.PrincipalSid)) throw new ArgumentException("A snapshot principal is required.");
        options.Validate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.RootDirectory);
            await PruneCoreAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            var normalized = snapshot with { IntegrityHash = ComputeIntegrityHash(snapshot with { IntegrityHash = string.Empty }) };
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > options.MaxSnapshotBytes) throw new InvalidDataException("The snapshot exceeded its bounded size.");
            await AtomicFileWriter.WriteAsync(PathFor(normalized.SnapshotId), json, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<SafetySnapshotVerificationResult> ReadAndVerifyAsync(
        string principalSid,
        string snapshotId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.RootDirectory);
        if (string.IsNullOrWhiteSpace(principalSid) || !IsValidId(snapshotId)) return new(SafetySnapshotState.Unavailable, false, null, "The snapshot identifier is invalid.");
        var path = PathFor(snapshotId);
        if (!File.Exists(path)) return new(SafetySnapshotState.Unavailable, false, null, "The requested snapshot is not available.");
        try
        {
            var info = new FileInfo(path);
            if (info.Length > options.MaxSnapshotBytes) return new(SafetySnapshotState.Corrupt, false, null, "The snapshot exceeded the storage bound.");
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<SafetySnapshotDocument>(json, JsonOptions);
            if (snapshot is null || !string.Equals(snapshot.SnapshotId, snapshotId, StringComparison.Ordinal)) return new(SafetySnapshotState.Corrupt, false, null, "The snapshot document is invalid.");
            if (!string.Equals(snapshot.PrincipalSid, principalSid, StringComparison.Ordinal)) return new(SafetySnapshotState.Mismatched, false, null, "The snapshot belongs to another authenticated principal.");
            var expected = ComputeIntegrityHash(snapshot with { IntegrityHash = string.Empty });
            if (!FixedTimeEquals(expected, snapshot.IntegrityHash)) return new(SafetySnapshotState.Corrupt, false, null, "The snapshot integrity check failed.");
            if (now >= snapshot.ExpiresAtUtc) return new(SafetySnapshotState.Expired, false, snapshot, "The snapshot has expired and cannot authorize repair.");
            return new(SafetySnapshotState.Verified, true, snapshot, "The snapshot is fresh, principal-scoped, and integrity verified.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(SafetySnapshotState.Corrupt, false, null, "The snapshot could not be verified safely."); }
    }

    public async Task<int> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        options.Validate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.RootDirectory);
            return await PruneCoreAsync(now, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public static string ComputeIntegrityHash(SafetySnapshotDocument snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot with { IntegrityHash = string.Empty }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private async Task<int> PruneCoreAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.RootDirectory)) return 0;
        var files = new List<(FileInfo Info, string PrincipalSid)>();
        foreach (var path in Directory.EnumerateFiles(options.RootDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (!IsValidId(Path.GetFileNameWithoutExtension(path))) continue;
            try
            {
                var info = new FileInfo(path);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var snapshot = JsonSerializer.Deserialize<SafetySnapshotDocument>(json, JsonOptions);
                if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.PrincipalSid))
                {
                    try { File.Delete(path); } catch { }
                    continue;
                }
                files.Add((info, snapshot.PrincipalSid));
            }
            catch
            {
                try { File.Delete(path); } catch { }
            }
        }
        var removed = 0;
        foreach (var group in files.GroupBy(file => file.PrincipalSid, StringComparer.Ordinal))
        {
            var ordered = group.OrderByDescending(file => file.Info.LastWriteTimeUtc).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remove = index >= options.MaxSnapshots;
                if (!remove)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(ordered[index].Info.FullName, cancellationToken).ConfigureAwait(false);
                        var snapshot = JsonSerializer.Deserialize<SafetySnapshotDocument>(json, JsonOptions);
                        remove = snapshot is null || now - snapshot.ExpiresAtUtc >= options.Lifetime;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch { remove = true; }
                }

                if (remove)
                {
                    try { File.Delete(ordered[index].Info.FullName); removed++; } catch { }
                }
            }
        }
        return removed;
    }

    private string PathFor(string snapshotId) => Path.Combine(options.RootDirectory, snapshotId + ".json");

    private static void ValidateId(string? value)
    {
        if (!IsValidId(value)) throw new ArgumentException("A canonical opaque snapshot identifier is required.", nameof(value));
    }

    private static bool IsValidId(string? value) => value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
