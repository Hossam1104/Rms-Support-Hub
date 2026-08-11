namespace RmsSupportHub.Pos.Agent.Security;

public static class AgentCors
{
    public const string PolicyName = "PosAgentBrowser";

    public static readonly string[] AllowedMethods = ["GET", "POST"];

    public static readonly string[] AllowedHeaders =
    [
        "Accept",
        "Content-Type",
        "X-Correlation-Id",
        MutationTokenContract.HeaderName
    ];
}
