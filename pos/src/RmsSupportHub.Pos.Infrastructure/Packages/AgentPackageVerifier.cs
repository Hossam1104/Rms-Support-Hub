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
        options.Validate();
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;
        var archivePath = ArchivePath(manifest);
        if (!File.Exists(archivePath)) return new(AgentPackageVerificationState.Unknown, ["package_artifact_unavailable"], "The server-owned package artifact is not available.");
        try
        {
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

    public string ArchivePath(AgentPackageManifest manifest) =>
        Path.Combine(options.PackageRoot, "available", manifest.PackageId + "-" + manifest.Version + ".zip");
}
