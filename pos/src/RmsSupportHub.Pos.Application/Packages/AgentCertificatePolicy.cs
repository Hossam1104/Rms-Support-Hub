using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Packages;

/// <summary>
/// Pure certificate lifecycle policy. Store access, issuance, rotation, and removal are platform
/// seams; this policy decides whether a candidate is safe before those seams are called.
/// </summary>
public sealed class AgentCertificatePolicy
{
    public const string ExpectedProvider = "Microsoft Software Key Storage Provider";
    public const string ExpectedStoreLocation = "LocalMachine";
    public const string ExpectedOwnershipMarker = "RmsSupportAgent certificate v1";
    public const string EnterpriseOwnershipMarker = "RmsSupportAgent enterprise-managed certificate v1";

    public AgentCertificateAssessment Assess(
        AgentCertificateEvidence evidence,
        DateTimeOffset nowUtc,
        TimeSpan renewalWindow = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        renewalWindow = renewalWindow == default ? TimeSpan.FromDays(30) : renewalWindow;
        if (!string.Equals(evidence.DnsName, "rms-pos-agent.localhost", StringComparison.OrdinalIgnoreCase)) return Reject("certificate_san_invalid", "The certificate does not contain the exact canonical Agent DNS name.");
        if (!string.Equals(evidence.StoreLocation, ExpectedStoreLocation, StringComparison.Ordinal)) return Reject("certificate_store_invalid", "The Agent certificate must be machine-scoped.");
        if (!evidence.HasPrivateKey) return Reject("private_key_missing", "The Agent certificate has no private key.");
        if (!string.Equals(evidence.KeyStorageProvider, ExpectedProvider, StringComparison.Ordinal)) return Reject("certificate_provider_invalid", "The Agent certificate is not backed by the expected CNG provider.");
        if (evidence.IsExportable) return Reject("private_key_exportable", "The Agent private key must be non-exportable.");
        if (!evidence.HasServerAuthenticationEku) return Reject("server_authentication_eku_missing", "The Agent certificate lacks the Server Authentication EKU.");
        if (evidence.OwnershipMarker is not (ExpectedOwnershipMarker or EnterpriseOwnershipMarker)) return Reject("certificate_ownership_unproven", "Certificate ownership is not proven; the existing certificate must not be replaced or removed.");
        if (string.IsNullOrWhiteSpace(evidence.Thumbprint)) return Reject("certificate_thumbprint_missing", "The owned Agent certificate thumbprint is unavailable.");
        if (evidence.NotBeforeUtc > nowUtc || evidence.NotAfterUtc < nowUtc) return Reject("certificate_expired_or_not_yet_valid", "The Agent certificate is not currently valid.");

        var renewalRequired = evidence.NotAfterUtc - nowUtc <= renewalWindow;
        return new(true, renewalRequired ? "renewal_due" : "valid", renewalRequired ? "The owned Agent certificate is valid but within its renewal window." : "The owned Agent certificate satisfies the machine-scoped Agent TLS policy.", true, renewalRequired);

        static AgentCertificateAssessment Reject(string code, string detail) => new(false, code, detail, false, false);
    }

    public bool CanRemove(AgentCertificateEvidence evidence, string expectedThumbprint) =>
        evidence is not null
        && !string.IsNullOrWhiteSpace(expectedThumbprint)
        && string.Equals(evidence.Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase)
        && string.Equals(evidence.OwnershipMarker, ExpectedOwnershipMarker, StringComparison.Ordinal);
}
