namespace RmsSupportHub.Pos.Domain.Models;

public enum AgentResourceOwnershipState
{
    Missing,
    Owned,
    Unowned,
    Conflict,
    Unknown
}

/// <summary>SCM evidence supplied by a platform adapter; paths are never transport fields.</summary>
public sealed record AgentServiceEvidence(
    string ServiceName,
    string? DisplayName,
    string? Description,
    string? BinaryPath,
    string? PackageId,
    string? PackageVersion,
    string? OwnershipMarker,
    string? BinarySha256,
    AgentResourceOwnershipState State)
{
    /// <summary>SCM configuration evidence; these properties are not browser transport fields.</summary>
    public string? ServiceAccount { get; init; }

    public string? StartType { get; init; }

    public bool? HasArguments { get; init; }
}

public sealed record AgentOwnershipAssessment(
    AgentResourceOwnershipState State,
    string Code,
    string Detail,
    AgentServiceEvidence Evidence);

public enum AgentMigrationAction
{
    NoOp,
    MigrateOwnedLegacy,
    Conflict
}

public sealed record AgentMigrationDecision(
    AgentMigrationAction Action,
    string Code,
    string Detail,
    AgentOwnershipAssessment PermanentAssessment,
    IReadOnlyList<AgentOwnershipAssessment> LegacyAssessments);

public sealed record AgentLegacyServiceDefinition(
    string ServiceName,
    string DisplayName,
    IReadOnlyList<string> AllowedOwnershipMarkers);

public static class AgentLegacyServiceCatalog
{
    public static IReadOnlyList<AgentLegacyServiceDefinition> Definitions { get; } =
    [
        new(
            "RmsSupportHub.Pos.Agent",
            "RMS+ POS Agent (Testing)",
            ["RmsSupportHub INT-13 Testing Provisioning v1", "RmsSupportHub INT-13P Testing Provisioning v1"]),
        new(
            "RmsSupportHub.Pos.Int13.TestService",
            "RMS+ INT-13 Disposable Testing Service",
            ["RmsSupportHub INT-13 Testing Provisioning v1", "RmsSupportHub INT-13P Testing Provisioning v1"])
    ];

    public static bool TryGet(string? serviceName, out AgentLegacyServiceDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.ServiceName, serviceName, StringComparison.Ordinal))!;
        return definition is not null;
    }
}

public sealed record AgentPrivateKeySecurityEvidence(
    bool IsMachineKey,
    bool IsCngKey,
    string? ProviderName,
    bool IsExportable,
    bool KeyFileExists,
    bool KeyFileIsReparsePoint,
    bool OwnerTrusted,
    bool AccessRulesProtected,
    bool LocalSystemReadAccess,
    bool HasUnsafeAllowRules)
{
    public bool IsTrusted => IsMachineKey
        && IsCngKey
        && !IsExportable
        && KeyFileExists
        && !KeyFileIsReparsePoint
        && OwnerTrusted
        && AccessRulesProtected
        && LocalSystemReadAccess
        && !HasUnsafeAllowRules;
}

public sealed record AgentCertificateEvidence(
    string? Thumbprint,
    string? DnsName,
    bool HasPrivateKey,
    string? KeyStorageProvider,
    bool IsExportable,
    bool HasServerAuthenticationEku,
    string? OwnershipMarker,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc,
    string StoreLocation,
    AgentPrivateKeySecurityEvidence? PrivateKeySecurity = null);

public sealed record AgentCertificateAssessment(
    bool Valid,
    string Code,
    string Detail,
    bool Owned,
    bool RenewalRequired);

public static class AgentOwnershipPolicy
{
    public static AgentOwnershipAssessment AssessPermanent(
        AgentServiceEvidence evidence,
        string expectedInstallRoot,
        string expectedPackageId,
        string? expectedVersion = null,
        string? expectedBinarySha256 = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!string.Equals(evidence.ServiceName, AgentProductIdentity.PermanentServiceName, StringComparison.Ordinal))
        {
            return Reject("permanent_service_name_mismatch", "The inspected service is not the permanent RMS Support Agent.");
        }

        if (!string.Equals(evidence.DisplayName, AgentProductIdentity.ServiceDisplayName, StringComparison.Ordinal)
            || !string.Equals(evidence.Description, AgentProductIdentity.ServiceDescription, StringComparison.Ordinal))
        {
            return Reject("permanent_service_identity_mismatch", "The permanent Agent service identity does not match the product contract.");
        }

        if (!IsOwnedBinary(evidence.BinaryPath, expectedInstallRoot)
            || !string.Equals(evidence.PackageId, expectedPackageId, StringComparison.Ordinal)
            || !string.Equals(evidence.OwnershipMarker, AgentProductIdentity.ProductId, StringComparison.Ordinal))
        {
            return Reject("permanent_service_ownership_unproven", "The permanent Agent service cannot be independently proven to be Support Hub-owned.");
        }

        if (expectedVersion is not null && !string.Equals(evidence.PackageVersion, expectedVersion, StringComparison.Ordinal))
        {
            return Reject("permanent_service_version_mismatch", "The owned permanent Agent version does not match the requested package.");
        }

        if (expectedBinarySha256 is not null && !string.Equals(evidence.BinarySha256, expectedBinarySha256, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("permanent_service_hash_mismatch", "The permanent Agent binary does not match its owned package manifest.");
        }

        return new(AgentResourceOwnershipState.Owned, "owned", "The permanent RMS Support Agent is independently owned and matches the product contract.", evidence);

        AgentOwnershipAssessment Reject(string code, string detail) =>
            new(AgentResourceOwnershipState.Conflict, code, detail, evidence with { State = AgentResourceOwnershipState.Conflict });
    }

    public static AgentOwnershipAssessment AssessLegacy(
        AgentServiceEvidence evidence,
        AgentLegacyServiceDefinition definition,
        string expectedInstallRoot,
        string? expectedPackageId = null,
        string? expectedBinarySha256 = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.Equals(evidence.ServiceName, definition.ServiceName, StringComparison.Ordinal)
            || !string.Equals(evidence.DisplayName, definition.DisplayName, StringComparison.Ordinal)
            || !IsOwnedBinary(evidence.BinaryPath, expectedInstallRoot)
            || !definition.AllowedOwnershipMarkers.Contains(evidence.OwnershipMarker ?? string.Empty, StringComparer.Ordinal)
            || expectedPackageId is not null && !string.Equals(evidence.PackageId, expectedPackageId, StringComparison.Ordinal)
            || expectedBinarySha256 is not null && !string.Equals(evidence.BinarySha256, expectedBinarySha256, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                AgentResourceOwnershipState.Unowned,
                "legacy_service_ownership_unproven",
                "The known legacy service is present, but independent Support Hub ownership evidence is incomplete or mismatched. No migration is permitted.",
                evidence with { State = AgentResourceOwnershipState.Unowned });
        }

        return new(
            AgentResourceOwnershipState.Owned,
            "legacy_service_owned",
            "The known legacy service is independently proven to be Support Hub-owned and may be considered for bounded migration.",
            evidence with { State = AgentResourceOwnershipState.Owned });
    }

    private static bool IsOwnedBinary(string? binaryPath, string expectedInstallRoot)
    {
        if (string.IsNullOrWhiteSpace(binaryPath) || string.IsNullOrWhiteSpace(expectedInstallRoot)) return false;
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedInstallRoot));
            var binary = Path.GetFullPath(binaryPath.Trim().Trim('"'));
            return binary.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !binary.Contains("..", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
