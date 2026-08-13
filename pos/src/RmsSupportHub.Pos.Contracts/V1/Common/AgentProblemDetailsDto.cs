using System.Text.Json.Serialization;

namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// Safe, machine-readable problem details returned by the POS Agent foundation boundary.
/// It deliberately contains no principal, path, credential, or machine-internal data.
/// </summary>
public sealed record AgentProblemDetailsDto(
    /// <summary>Stable problem type identifier.</summary>
    [property: JsonPropertyName("type")] string Type,
    /// <summary>Safe human-readable reason for the rejected request.</summary>
    [property: JsonPropertyName("title")] string Title,
    /// <summary>HTTP status code returned by the Agent.</summary>
    [property: JsonPropertyName("status")] int Status,
    /// <summary>Optional stable machine-readable Agent problem code.</summary>
    [property: JsonPropertyName("code")] string? Code = null,
    /// <summary>Optional request correlation identifier for diagnostics.</summary>
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null);
