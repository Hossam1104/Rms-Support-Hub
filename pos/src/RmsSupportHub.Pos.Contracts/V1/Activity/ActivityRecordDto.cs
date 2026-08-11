namespace RmsSupportHub.Pos.Contracts.V1.Activity;

/// <summary>One row of the paged <c>GET /api/v1/activity</c> timeline. Never contains a secret.</summary>
public sealed record ActivityRecordDto(
    string ActivityId,
    DateTimeOffset AtUtc,
    string Category,
    string Summary,
    string? CorrelationId,
    bool IsDestructive);
