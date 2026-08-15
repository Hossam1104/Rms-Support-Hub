using System.Security.Cryptography;
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
}
