namespace RmsSupportHub.Pos.Application.Diagnostics;

public static class RmsInstallationDiscoveryFailureCodes
{
    public const string AuditUnavailable = "audit_unavailable";
}

/// <summary>Safe typed propagation for the mandatory-audit failure boundary.</summary>
public sealed class RmsInstallationDiscoveryAuditUnavailableException()
    : InvalidOperationException("The RMS installation discovery query could not be completed because its audit record was unavailable.");
