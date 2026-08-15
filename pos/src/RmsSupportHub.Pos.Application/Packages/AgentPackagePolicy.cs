using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Packages;

/// <summary>
/// Portable package-manifest policy. It validates the semantic package boundary before any
/// Windows staging or service/certificate operation is considered.
/// </summary>
public sealed class AgentPackagePolicy : IAgentPackagePolicy
{
    public const long MaxPackageBytes = 256L * 1024 * 1024;
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const int MaxFiles = 256;

    private static readonly Regex SafeToken = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedAclRequirements = new(StringComparer.Ordinal)
    {
        "AgentServiceReadWrite",
        "AgentServiceExecute",
        "AgentCertificatePrivateKey"
    };
    private static readonly HashSet<string> AllowedCertificateRequirements = new(StringComparer.Ordinal)
    {
        "AgentHttpsCertificate",
        "AgentSigningCertificate"
    };

    public AgentPackageValidationResult ValidateManifest(AgentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var blockers = new List<string>();

        if (!SafeToken.IsMatch(manifest.PackageId ?? string.Empty)) blockers.Add("package_id_invalid");
        if (!SafeToken.IsMatch(manifest.Version ?? string.Empty)) blockers.Add("package_version_invalid");
        if (!string.Equals(manifest.SupportedOperatingSystem, "Windows", StringComparison.OrdinalIgnoreCase)) blockers.Add("os_not_supported");
        if (!string.Equals(manifest.SupportedRuntime, "net10.0-windows", StringComparison.OrdinalIgnoreCase)) blockers.Add("runtime_not_supported");
        if (!string.Equals(manifest.ServiceIdentity, "LocalSystem", StringComparison.Ordinal)) blockers.Add("service_identity_invalid");
        if (!string.Equals(manifest.ScmName, "RmsSupportHub.Pos.Agent", StringComparison.Ordinal)) blockers.Add("scm_name_invalid");
        if (!string.Equals(manifest.SignatureAlgorithm, "SHA256withRSA", StringComparison.Ordinal)) blockers.Add("signature_algorithm_invalid");
        if (string.IsNullOrWhiteSpace(manifest.SignerDisplayName) || manifest.SignerDisplayName.Length > 128) blockers.Add("signer_invalid");
        if (!IsSha256(manifest.PackageSha256)) blockers.Add("package_hash_invalid");
        if (manifest.PackageSizeBytes is < 1 or > MaxPackageBytes) blockers.Add("package_size_invalid");
        if (manifest.Files is null || manifest.Files.Count is < 1 or > MaxFiles) blockers.Add("file_count_invalid");

        var totalBytes = 0L;
        var logicalNames = new HashSet<string>(StringComparer.Ordinal);
        var relativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files ?? [])
        {
            if (file is null)
            {
                blockers.Add("file_entry_invalid");
                continue;
            }

            var logicalName = file.LogicalName ?? string.Empty;
            if (!SafeToken.IsMatch(logicalName) || !logicalNames.Add(logicalName)) blockers.Add("file_logical_name_invalid");
            var normalizedRelativePath = file.RelativePath?.Replace('\\', '/') ?? string.Empty;
            if (!IsSafeRelativePath(file.RelativePath) || !relativePaths.Add(normalizedRelativePath)) blockers.Add("file_path_invalid");
            if (file.SizeBytes is < 0 or > MaxFileBytes) blockers.Add("file_size_invalid");
            if (!IsSha256(file.Sha256)) blockers.Add("file_hash_invalid");
            totalBytes = totalBytes > MaxPackageBytes - Math.Max(0, file.SizeBytes)
                ? MaxPackageBytes + 1
                : totalBytes + Math.Max(0, file.SizeBytes);
        }

        if (totalBytes > MaxPackageBytes) blockers.Add("file_total_size_invalid");
        if ((manifest.AclRequirements ?? []).Any(item => !AllowedAclRequirements.Contains(item))) blockers.Add("acl_requirement_invalid");
        if ((manifest.CertificateRequirements ?? []).Any(item => !AllowedCertificateRequirements.Contains(item))) blockers.Add("certificate_requirement_invalid");
        if (manifest.RollbackAvailable && string.IsNullOrWhiteSpace(manifest.PreviousVersion)) blockers.Add("rollback_metadata_invalid");

        return blockers.Count == 0
            ? new(AgentPackageVerificationState.Verified, [], "The package manifest passed the Agent policy checks.")
            : new(AgentPackageVerificationState.Rejected, blockers.Distinct(StringComparer.Ordinal).ToArray(), "The package manifest was rejected by the Agent policy.");
    }

    private static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 || Path.IsPathRooted(path)) return false;
        if (path.Contains(':', StringComparison.Ordinal) || path.Contains('\0')) return false;
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/').Any(segment => segment is "" or "." or "..")) return false;
        return normalized.All(character => !char.IsControl(character));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
