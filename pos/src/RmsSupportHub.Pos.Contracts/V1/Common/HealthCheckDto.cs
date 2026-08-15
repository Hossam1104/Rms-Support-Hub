namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>One bounded, read-only health result with an explicit unknown state.</summary>
public sealed record HealthCheckDto(
    /// <summary>Stable server-owned check code.</summary>
    string Code,
    /// <summary>Current classification of the check.</summary>
    HealthState State,
    /// <summary>Safe operator summary without secrets, paths, SQL, or raw exceptions.</summary>
    string Summary,
    /// <summary>UTC time at which this check was evaluated.</summary>
    DateTimeOffset CheckedAtUtc,
    /// <summary>Optional safe next action; it is guidance only and does not execute a repair.</summary>
    string? Remediation);
