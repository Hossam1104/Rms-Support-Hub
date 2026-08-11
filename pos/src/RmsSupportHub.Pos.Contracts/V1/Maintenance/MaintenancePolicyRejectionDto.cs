namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>Sanitized server-policy rejection. It contains a logical target ID, never a host path.</summary>
public sealed record MaintenancePolicyRejectionDto(
    string TargetId,
    string Code,
    string Reason);
