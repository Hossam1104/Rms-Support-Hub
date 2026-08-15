using System.IO.Compression;
using System.Security.Cryptography;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public interface IAgentPackageInstallationPlatform
{
    Task<bool> PrepareAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    Task<bool> VerifyHealthAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    Task<bool> RollbackAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default);
}

/// <summary>Conservative platform seam; real service/ACL/certificate ownership is injected here.</summary>
public sealed class WindowsAgentPackageInstallationPlatform : IAgentPackageInstallationPlatform
{
    public Task<bool> PrepareAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> VerifyHealthAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> RollbackAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

/// <summary>
/// Versioned package boundary. It can stage only exact manifest files under the fixed Agent root,
/// verifies each hash again, and reports rollback/recovery truth. Activation is delegated to the
/// typed platform seam and is never inferred from staging success.
/// </summary>
public sealed class FileAgentPackageLifecycle(
    AgentPackageOptions options,
    FileAgentPackageVerifier verifier,
    IAgentPackageInstallationPlatform platform) : IAgentPackageLifecycle
{
    public async Task<AgentPackageExecutionResult> ExecuteAsync(AgentPackageExecutionRequest request, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        var validation = request.Operation == AgentPackageOperationKind.Uninstall
            ? await verifier.VerifyInstalledAsync(request.Manifest, cancellationToken).ConfigureAwait(false)
            : await verifier.VerifyAsync(request.Manifest, cancellationToken).ConfigureAwait(false);
        if (validation.State != AgentPackageVerificationState.Verified) return new(AgentPackageOperationState.Failed, false, false, false, validation.Detail);

        var stagingRoot = Path.Combine(options.PackageRoot, "staging", Guid.NewGuid().ToString("N"));
        try
        {
            if (!await platform.PrepareAsync(request.Manifest, cancellationToken).ConfigureAwait(false)) return new(AgentPackageOperationState.Failed, false, false, true, "The Agent platform precondition failed; package activation was not attempted.");
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(stagingRoot);
            if (request.Operation != AgentPackageOperationKind.Uninstall)
            {
                await ExtractExactFilesAsync(verifier.ArchivePath(request.Manifest), stagingRoot, request.Manifest, cancellationToken).ConfigureAwait(false);
            }
            if (!await platform.ActivateAsync(stagingRoot, request.Manifest, cancellationToken).ConfigureAwait(false))
            {
                var rollback = await platform.RollbackAsync(request.Manifest, cancellationToken).ConfigureAwait(false);
                return rollback
                    ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Activation failed; the prior Agent package was restored.")
                    : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Activation failed and rollback could not be confirmed; recovery is required.");
            }

            if (!await platform.VerifyHealthAsync(request.Manifest, cancellationToken).ConfigureAwait(false))
            {
                var rollback = await platform.RollbackAsync(request.Manifest, cancellationToken).ConfigureAwait(false);
                return rollback
                    ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Post-activation health failed; the prior Agent package was restored.")
                    : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Post-activation health failed and rollback could not be confirmed; recovery is required.");
            }

            return new(AgentPackageOperationState.Completed, false, false, false, "The verified Agent package was activated and health was confirmed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var rollback = await SafeRollbackAsync(request.Manifest, CancellationToken.None).ConfigureAwait(false);
            return rollback
                ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Package activation was cancelled; the prior package was restored.")
                : new(AgentPackageOperationState.RecoveryRequired, true, false, true, "Package activation was cancelled and recovery could not be confirmed.");
        }
        catch
        {
            var rollback = await SafeRollbackAsync(request.Manifest, CancellationToken.None).ConfigureAwait(false);
            return rollback
                ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Package activation failed; the prior package was restored.")
                : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Package activation failed and rollback could not be confirmed; recovery is required.");
        }
        finally
        {
            try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true); } catch { }
        }
    }

    private static async Task ExtractExactFilesAsync(string archivePath, string stagingRoot, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        var expected = manifest.Files.ToDictionary(file => file.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count != expected.Count) throw new InvalidDataException("The package archive contains an unexpected file set.");
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (IsArchiveLink(entry)
                || normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.Split('/').Any(segment => segment is "" or "." or "..")
                || !seen.Add(normalized)
                || !expected.TryGetValue(normalized, out var file)) throw new InvalidDataException("The package archive contains an unsafe or unexpected path.");
            var target = Path.GetFullPath(Path.Combine(stagingRoot, normalized));
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingRoot)) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The package archive escaped the staging root.");
            var targetDirectory = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(targetDirectory);
            if (HasReparsePointOnPath(stagingRoot, targetDirectory) || File.Exists(target) && IsReparsePoint(target))
            {
                throw new InvalidDataException("The package staging path contains a reparse point.");
            }
            await using (var source = entry.Open())
            await using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await CopyExactAsync(source, destination, file.SizeBytes, cancellationToken).ConfigureAwait(false);
            }
            if (IsReparsePoint(target)) throw new InvalidDataException("The staged package file became a reparse point.");
            var actualSize = new FileInfo(target).Length;
            if (actualSize != file.SizeBytes || !string.Equals(await ComputeHashAsync(target, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The staged package file did not match its manifest.");
        }

        if (seen.Count != expected.Count) throw new InvalidDataException("The package archive did not contain every manifest file.");
    }

    private static async Task CopyExactAsync(Stream source, Stream destination, long declaredSize, CancellationToken cancellationToken)
    {
        if (declaredSize < 0) throw new InvalidDataException("The package manifest declared a negative file size.");
        var buffer = new byte[64 * 1024];
        long copied = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (copied > declaredSize - read)
            {
                throw new InvalidDataException("The package archive streamed beyond the declared manifest size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
        }

        if (copied != declaredSize) throw new InvalidDataException("The package archive ended before the declared manifest size.");
    }

    private async Task<bool> SafeRollbackAsync(AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        try { return await platform.RollbackAsync(manifest, cancellationToken).ConfigureAwait(false); } catch { return false; }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static bool IsArchiveLink(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        return ((entry.ExternalAttributes >> 16) & UnixFileTypeMask) == UnixSymbolicLink;
    }

    private static bool HasReparsePointOnPath(string root, string targetDirectory)
    {
        try
        {
            var current = Path.GetFullPath(targetDirectory);
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            while (current.Length >= normalizedRoot.Length)
            {
                if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)) return true;
                if (string.Equals(current, normalizedRoot, StringComparison.OrdinalIgnoreCase)) return false;
                var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
                if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) return false;
                current = parent;
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    private static bool IsReparsePoint(string path)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch { return true; }
    }
}
