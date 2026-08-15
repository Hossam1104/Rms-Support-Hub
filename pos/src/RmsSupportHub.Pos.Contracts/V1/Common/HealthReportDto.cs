namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>Aggregate read-only POS health report composed from typed local evidence.</summary>
public sealed record HealthReportDto(
    /// <summary>Conservative aggregate state across all returned checks.</summary>
    HealthState OverallState,
    /// <summary>Safe aggregate summary suitable for the compact operator header.</summary>
    string Summary,
    /// <summary>UTC time at which this report was evaluated.</summary>
    DateTimeOffset CheckedAtUtc,
    /// <summary>Bounded individual health checks.</summary>
    IReadOnlyList<HealthCheckDto> Checks);
