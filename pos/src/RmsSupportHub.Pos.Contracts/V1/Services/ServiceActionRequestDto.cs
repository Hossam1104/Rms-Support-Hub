namespace RmsSupportHub.Pos.Contracts.V1.Services;

/// <summary>
/// <c>POST /api/v1/services/{serviceId}/actions</c> request body. <c>IdempotencyKey</c> lets a
/// retried submit avoid starting duplicate work (plan section 5.1).
/// </summary>
public sealed record ServiceActionRequestDto(
    /// <summary>One of the explicit Start, Stop, or Restart service operations.</summary>
    ServiceActionKind Action,
    /// <summary>Bounded caller-generated key used to make one service action repeat-safe.</summary>
    string IdempotencyKey);
