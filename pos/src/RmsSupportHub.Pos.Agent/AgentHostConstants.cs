namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Immutable transport identity for the installed per-device Agent. These values are deliberately
/// not configuration-bound: changing them would create a second origin or a discovery protocol.
/// </summary>
public static class AgentHostConstants
{
    public const string CanonicalHost = "rms-pos-agent.localhost";

    public const int Port = 5001;

    public const string CanonicalOrigin = "https://rms-pos-agent.localhost:5001";

    public const string ApiVersion = "1.0";

    public const string IntegrationTestEnvironment = "IntegrationTest";
}
