using System.Security.Cryptography;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public interface IAgentPackageSignatureVerifier
{
    Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default);
}

/// <summary>Default verifier fails closed until a trusted machine-owned signing certificate is provisioned.</summary>
public sealed class MachineCertificatePackageSignatureVerifier : IAgentPackageSignatureVerifier
{
    public Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}

public sealed class FileAgentPackageVerifier(
    AgentPackageOptions options,
    IAgentPackagePolicy policy,
    IAgentPackageSignatureVerifier signatureVerifier) : IAgentPackageVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentPackageValidationResult> VerifyAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;
        var archivePath = ArchivePath(manifest);
        try
        {
            if (!File.Exists(archivePath)
                || File.GetAttributes(archivePath).HasFlag(FileAttributes.ReparsePoint)) return new(AgentPackageVerificationState.Unknown, ["package_artifact_unavailable"], "The server-owned package artifact is not available.");
            var info = new FileInfo(archivePath);
            if (info.Length != manifest.PackageSizeBytes || info.Length > options.MaxPackageBytes) return new(AgentPackageVerificationState.Rejected, ["package_size_mismatch"], "The package size does not match its manifest.");
            await using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            if (!string.Equals(hash, manifest.PackageSha256, StringComparison.OrdinalIgnoreCase)) return new(AgentPackageVerificationState.Rejected, ["package_checksum_mismatch"], "The package checksum does not match its manifest.");
            if (!await signatureVerifier.VerifyAsync(manifest, archivePath, cancellationToken).ConfigureAwait(false)) return new(AgentPackageVerificationState.Rejected, ["package_signature_unverified"], "The package signature could not be verified against a trusted machine-owned certificate.");
            return new(AgentPackageVerificationState.Verified, [], "The package signature, checksum, size, and manifest policy are verified.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(AgentPackageVerificationState.Unknown, ["package_verification_unknown"], "The package could not be verified safely."); }
    }

    public async Task<AgentPackageValidationResult> VerifyInstalledAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.InstallationRoot));
            var manifestPath = Path.Combine(root, "manifest.json");
            if (!File.Exists(manifestPath)
                || File.GetAttributes(manifestPath).HasFlag(FileAttributes.ReparsePoint))
            {
                return new(AgentPackageVerificationState.Unknown, ["installed_package_unavailable"], "The installed Agent manifest is not available.");
            }

            if (new FileInfo(manifestPath).Length > 1024 * 1024)
            {
                return new(AgentPackageVerificationState.Rejected, ["installed_manifest_too_large"], "The installed Agent manifest exceeds the bounded control-file size.");
            }

            AgentPackageManifest? installedManifest;
            await using (var manifestStream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                installedManifest = await JsonSerializer.DeserializeAsync<AgentPackageManifest>(manifestStream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            if (installedManifest is null || !ManifestEquivalent(installedManifest, manifest))
            {
                return new(AgentPackageVerificationState.Rejected, ["installed_manifest_mismatch"], "The installed Agent manifest does not match the requested trusted package identity.");
            }

            var expectedPaths = manifest.Files
                .Select(file => file.RelativePath.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedDirectories = BuildExpectedDirectories(expectedPaths);
            var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingDirectories = new Stack<string>([root]);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDirectory = pendingDirectories.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(currentDirectory))
                {
                    if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint))
                    {
                        return new(AgentPackageVerificationState.Rejected, ["installed_reparse_point"], "The installed package contains a reparse point and cannot be verified safely.");
                    }

                    var relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
                    if (Directory.Exists(entry))
                    {
                        if (!expectedDirectories.Contains(relative))
                        {
                            return new(AgentPackageVerificationState.Rejected, ["installed_directory_set_mismatch"], "The installed package contains an unexpected directory.");
                        }

                        pendingDirectories.Push(entry);
                        continue;
                    }

                    if (string.Equals(relative, "manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!expectedPaths.Contains(relative) || !actualPaths.Add(relative) || actualPaths.Count > AgentPackagePolicy.MaxFiles)
                    {
                        return new(AgentPackageVerificationState.Rejected, ["installed_file_set_mismatch"], "The installed package contains a file outside its signed manifest set.");
                    }
                }
            }

            if (!expectedPaths.SetEquals(actualPaths))
            {
                return new(AgentPackageVerificationState.Rejected, ["installed_file_set_mismatch"], "The installed package does not contain exactly the signed manifest file set.");
            }

            foreach (var file in manifest.Files)
            {
                if (!IsSafeRelativePath(file.RelativePath))
                {
                    return new(AgentPackageVerificationState.Rejected, ["installed_file_path_invalid"], "The installed manifest contains an unsafe relative file path.");
                }

                var target = Path.GetFullPath(Path.Combine(root, file.RelativePath));
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(target)
                    || HasReparsePointOnPath(root, Path.GetDirectoryName(target)!)
                    || File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))
                {
                    return new(AgentPackageVerificationState.Rejected, ["installed_file_unavailable"], "The installed package file boundary could not be verified safely.");
                }

                var info = new FileInfo(target);
                if (info.Length != file.SizeBytes
                    || !string.Equals(await ComputeHashAsync(target, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new(AgentPackageVerificationState.Rejected, ["installed_file_checksum_mismatch"], "The installed package file does not match its recorded manifest.");
                }
            }

            return new(AgentPackageVerificationState.Verified, [], "The installed Agent manifest and owned file hashes are verified.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(AgentPackageVerificationState.Unknown, ["installed_package_verification_unknown"], "The installed package could not be verified safely."); }
    }

    public string ArchivePath(AgentPackageManifest manifest) =>
        Path.Combine(options.PackageRoot, "available", manifest.PackageId + "-" + manifest.Version + ".zip");

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !Path.IsPathFullyQualified(path)
        && !path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")
        && !path.Any(char.IsControl);

    private static bool HasReparsePointOnPath(string root, string targetDirectory)
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

        return true;
    }

    private static HashSet<string> BuildExpectedDirectories(IEnumerable<string> paths)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 1; index < segments.Length; index++)
            {
                directories.Add(string.Join('/', segments.Take(index)));
            }
        }

        return directories;
    }

    private static bool ManifestEquivalent(AgentPackageManifest left, AgentPackageManifest right)
    {
        if (!string.Equals(left.PackageId, right.PackageId, StringComparison.Ordinal)
            || !string.Equals(left.Version, right.Version, StringComparison.Ordinal)
            || !string.Equals(left.SupportedOperatingSystem, right.SupportedOperatingSystem, StringComparison.Ordinal)
            || !string.Equals(left.SupportedRuntime, right.SupportedRuntime, StringComparison.Ordinal)
            || !string.Equals(left.ServiceDisplayName, right.ServiceDisplayName, StringComparison.Ordinal)
            || !string.Equals(left.ServiceIdentity, right.ServiceIdentity, StringComparison.Ordinal)
            || !string.Equals(left.ScmName, right.ScmName, StringComparison.Ordinal)
            || !string.Equals(left.SignatureAlgorithm, right.SignatureAlgorithm, StringComparison.Ordinal)
            || !string.Equals(left.SignerDisplayName, right.SignerDisplayName, StringComparison.Ordinal)
            || !string.Equals(left.ProductId, right.ProductId, StringComparison.Ordinal)
            || !string.Equals(left.Architecture, right.Architecture, StringComparison.Ordinal)
            || !string.Equals(left.ReleaseChannel, right.ReleaseChannel, StringComparison.Ordinal)
            || !string.Equals(left.Environment, right.Environment, StringComparison.Ordinal)
            || !string.Equals(left.PackageSha256, right.PackageSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(left.Signature, right.Signature, StringComparison.Ordinal)
            || left.SchemaVersion != right.SchemaVersion
            || !string.Equals(left.PreviousVersion, right.PreviousVersion, StringComparison.Ordinal)
            || left.RollbackAvailable != right.RollbackAvailable
            || left.PackageSizeBytes != right.PackageSizeBytes
            || !(left.AclRequirements ?? []).SequenceEqual(right.AclRequirements ?? [], StringComparer.Ordinal)
            || !(left.CertificateRequirements ?? []).SequenceEqual(right.CertificateRequirements ?? [], StringComparer.Ordinal))
        {
            return false;
        }

        var leftFiles = left.Files
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rightFiles = right.Files
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return leftFiles.SequenceEqual(rightFiles);
    }
}
