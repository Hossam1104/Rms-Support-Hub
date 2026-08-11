namespace RmsSupportHub.Pos.Contracts.V1.Operations;

/// <summary>One row of <c>GET /api/v1/operations</c>. <see cref="OperationId"/> is opaque.</summary>
public sealed record OperationSummaryDto(
    string OperationId,
    string OperationType,
    OperationState State,
    int ProgressPercent,
    string CurrentStage,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc);
