namespace RmsSupportHub.Pos.Contracts.V1.Services;

/// <summary>
/// Safe result for one allow-listed service action. It contains only typed outcome truth, a stable
/// safe code, operator guidance, and the Agent correlation identifier; it never contains an
/// exception, SID, credential, path, command, or raw service target.
/// </summary>
public sealed record ServiceActionResponseDto(
    /// <summary>Whether the action was not attempted, accepted, definitively rejected, or ambiguous.</summary>
    ServiceActionOutcome Outcome,
    /// <summary>Stable safe code for the outcome and operator guidance.</summary>
    string Code,
    /// <summary>Safe human-readable detail without implementation or target disclosure.</summary>
    string Detail,
    /// <summary>Agent-generated or request-supplied safe correlation identifier.</summary>
    string CorrelationId);
