using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public interface IAgentPackageSignatureVerifier
{
    Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies the manifest's canonical signature with a signer pinned by machine-owned trust
/// configuration. The manifest never selects a certificate, and the archive itself is bound by
/// the signed package hash in the canonical envelope.
/// </summary>
public sealed class MachineCertificatePackageSignatureVerifier : IAgentPackageSignatureVerifier
{
    private static readonly Regex StrictBase64 = new("^[A-Za-z0-9+/]+={0,2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly AgentPackageTrustOptions options;
    private readonly IAgentPackageSignerCertificateSource certificateSource;
    private readonly IAgentPackageSignerTrustValidator trustValidator;

    public MachineCertificatePackageSignatureVerifier()
        : this(new AgentPackageTrustOptions(), new LocalMachineAgentPackageSignerCertificateSource(), new X509ChainAgentPackageSignerTrustValidator())
    {
    }

    public MachineCertificatePackageSignatureVerifier(
        AgentPackageTrustOptions options,
        IAgentPackageSignerCertificateSource certificateSource,
        IAgentPackageSignerTrustValidator trustValidator)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.certificateSource = certificateSource ?? throw new ArgumentNullException(nameof(certificateSource));
        this.trustValidator = trustValidator ?? throw new ArgumentNullException(nameof(trustValidator));
        this.options.Validate();
    }

    public Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!string.Equals(manifest.SignatureAlgorithm, "SHA256withRSA", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(manifest.ReleaseChannel)
                || !string.Equals(manifest.ReleaseChannel, manifest.Environment, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(manifest.Signature)
                || manifest.Signature.Length > 16 * 1024
                || manifest.Signature.Length % 4 != 0
                || !StrictBase64.IsMatch(manifest.Signature))
            {
                return Task.FromResult(false);
            }

            var thumbprint = ResolveThumbprint(manifest.ReleaseChannel);
            if (thumbprint is null) return Task.FromResult(false);

            using var certificate = certificateSource.Find(thumbprint);
            if (certificate is null
                || !string.Equals(AgentPackageTrustOptions.Normalize(certificate.Thumbprint), thumbprint, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            // Production always requires the explicit chain/revocation decision. The option is
            // only a narrowly injected Testing/test seam and cannot downgrade Production.
            var requireTrustedChain = string.Equals(manifest.ReleaseChannel, AgentProductIdentity.ReleaseChannelProduction, StringComparison.Ordinal)
                || options.RequireTrustedChain;
            if (!trustValidator.IsTrusted(certificate, requireTrustedChain, out _)) return Task.FromResult(false);

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(manifest.Signature);
            }
            catch (FormatException)
            {
                return Task.FromResult(false);
            }

            if (signature.Length is 0 or > 16 * 1024) return Task.FromResult(false);
            using var rsa = certificate.GetRSAPublicKey();
            if (rsa is null) return Task.FromResult(false);

            var payload = AgentPackageCanonicalizer.Canonicalize(manifest);
            return Task.FromResult(rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private string? ResolveThumbprint(string channel)
    {
        var configured = options.GetConfiguredThumbprint(channel);
        if (configured is not null) return configured;

        try
        {
            var path = options.TrustConfigurationPath;
            if (!File.Exists(path)
                || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)
                || new FileInfo(path).Length is <= 0 or > 16 * 1024)
            {
                return null;
            }

            // The trust configuration file is machine-owned and never provisioned by this
            // application. Its ownership/ACL boundary must be verified before any value inside it is
            // trusted -- a writable trust configuration must never become authority.
            if (!ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(path)) return null;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (!TryReadConfiguredThumbprint(document.RootElement, "productionSignerThumbprint", out var production)
                || !TryReadConfiguredThumbprint(document.RootElement, "testingSignerThumbprint", out var testing))
            {
                return null;
            }

            try
            {
                AgentPackageTrustOptions.ValidateDistinctThumbprints(production, testing);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return channel == "Production" ? production : testing;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryReadConfiguredThumbprint(
        JsonElement root,
        string propertyName,
        out string? normalized)
    {
        normalized = null;
        if (!root.TryGetProperty(propertyName, out var value)) return true;
        if (value.ValueKind != JsonValueKind.String) return false;

        var raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        normalized = AgentPackageTrustOptions.Normalize(raw);
        return normalized is not null;
    }
}

public sealed class LocalMachineAgentPackageSignerCertificateSource : IAgentPackageSignerCertificateSource
{
    public X509Certificate2? Find(string thumbprint)
    {
        var normalized = AgentPackageTrustOptions.Normalize(thumbprint);
        if (normalized is null || !OperatingSystem.IsWindows()) return null;

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var certificate in store.Certificates)
            {
                if (string.Equals(AgentPackageTrustOptions.Normalize(certificate.Thumbprint), normalized, StringComparison.Ordinal))
                {
                    return new X509Certificate2(certificate);
                }
            }
        }
        catch (CryptographicException)
        {
        }
        catch (SecurityException)
        {
        }

        return null;
    }
}

public sealed class X509ChainAgentPackageSignerTrustValidator : IAgentPackageSignerTrustValidator
{
    private const string CodeSigningEku = "1.3.6.1.5.5.7.3.3";

    public bool IsTrusted(X509Certificate2 certificate, bool requireTrustedChain, out string failureCode)
    {
        failureCode = "unknown";
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        if (certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() < now)
        {
            failureCode = "signer_expired_or_not_yet_valid";
            return false;
        }

        var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>().Any(usage => string.Equals(usage.Value, CodeSigningEku, StringComparison.Ordinal)))
        {
            failureCode = "signer_code_signing_eku_missing";
            return false;
        }

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsage is not null && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            failureCode = "signer_digital_signature_usage_missing";
            return false;
        }

        if (!requireTrustedChain)
        {
            // This switch is intended only for an explicit injected Testing/test certificate seam;
            // it still requires a valid, purpose-constrained certificate and never enables a
            // Production fallback.
            failureCode = "testing_chain_not_required";
            return true;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        if (!chain.Build(certificate))
        {
            failureCode = "signer_chain_untrusted_or_revoked";
            return false;
        }

        failureCode = "trusted";
        return true;
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
        if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal))
        {
            return new(AgentPackageVerificationState.Rejected, ["package_channel_not_configured"], "The package release channel is not enabled for this Agent instance.");
        }
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;
        return await VerifyArtifactAsync(manifest, ArchivePath(manifest), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentPackageValidationResult> VerifyRollbackAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal))
        {
            return new(AgentPackageVerificationState.Rejected, ["package_channel_not_configured"], "The package release channel is not enabled for this Agent instance.");
        }
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;
        return await VerifyArtifactAsync(manifest, RollbackArchivePath(manifest), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentPackageValidationResult> VerifyRecoveryAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal))
        {
            return new(AgentPackageVerificationState.Rejected, ["package_channel_not_configured"], "The package release channel is not enabled for this Agent instance.");
        }
        var policyResult = policy.ValidateManifest(manifest);
        if (policyResult.State != AgentPackageVerificationState.Verified) return policyResult;
        return await VerifyArtifactAsync(manifest, RecoveryArchivePath(manifest), cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentPackageValidationResult> VerifyArtifactAsync(
        AgentPackageManifest manifest,
        string archivePath,
        CancellationToken cancellationToken)
    {
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
        if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal))
        {
            return new(AgentPackageVerificationState.Rejected, ["package_channel_not_configured"], "The package release channel is not enabled for this Agent instance.");
        }
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
        Path.Combine(options.AvailableRoot, manifest.PackageId + "-" + manifest.Version + ".zip");

    public string RollbackArchivePath(AgentPackageManifest manifest) =>
        Path.Combine(options.RollbackRoot, manifest.PackageId + "-" + manifest.Version + ".zip");

    public string RecoveryArchivePath(AgentPackageManifest manifest) =>
        Path.Combine(options.RecoveryRoot, manifest.PackageId + "-" + manifest.Version + ".zip");

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
            || !string.Equals(left.ServiceDescription ?? AgentProductIdentity.ServiceDescription, right.ServiceDescription ?? AgentProductIdentity.ServiceDescription, StringComparison.Ordinal)
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
            || left.PackageSizeBytes != right.PackageSizeBytes)
        {
            return false;
        }

        return (left.AclRequirements ?? []).OrderBy(value => value, StringComparer.Ordinal)
                   .SequenceEqual((right.AclRequirements ?? []).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)
            && (left.CertificateRequirements ?? []).OrderBy(value => value, StringComparer.Ordinal)
                   .SequenceEqual((right.CertificateRequirements ?? []).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)
            && left.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                   .SequenceEqual(right.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal));
    }
}
