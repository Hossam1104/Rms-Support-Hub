using System.Text.Json.Serialization;

namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// Safe, machine-readable problem details returned by the POS Agent foundation boundary.
/// It deliberately contains no principal, path, credential, or machine-internal data.
/// </summary>
public sealed record AgentProblemDetailsDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("code")] string? Code = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null);
