namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Immutable identity for the permanent, per-device RMS Support Agent. Deployment code may
/// discover a package or legacy service, but it may not change these values from browser input.
/// </summary>
public static class AgentProductIdentity
{
    public const string ProductId = "RmsSupportAgent";

    public const string PermanentServiceName = "RmsSupportAgent";

    public const string ServiceDisplayName = "RMS Support Agent";

    public const string ServiceDescription = "Local diagnostics, maintenance and repair agent for RMS Support Hub";

    public const string PackageSchema = "rms-support-agent.package.v1";

    public const string ReleaseChannelTesting = "Testing";

    public const string ReleaseChannelProduction = "Production";

    public const string EnvironmentTesting = "Testing";

    public const string EnvironmentProduction = "Production";

    public static IReadOnlyList<string> HistoricalTestingServiceNames { get; } =
    [
        "RmsSupportHub.Pos.Agent",
        "RmsSupportHub.Pos.Int13.TestService"
    ];

    public static bool IsHistoricalTestingService(string? serviceName) =>
        serviceName is not null && HistoricalTestingServiceNames.Contains(serviceName, StringComparer.Ordinal);

    public static bool IsRmsProductService(string? serviceName) => serviceName is
        RmsServiceCatalog.BranchServiceName
        or RmsServiceCatalog.CashierServiceName
        or RmsServiceCatalog.ServicesManagerServiceName;
}
