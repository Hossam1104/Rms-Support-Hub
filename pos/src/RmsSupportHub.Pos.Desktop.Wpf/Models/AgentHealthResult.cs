namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record AgentHealthResult(
    HealthViewState State,
    string? AgentStatus,
    string? IpcStatus,
    int? ProtocolVersion,
    bool? HubConnectivityRequired,
    string? CorrelationId,
    string ErrorCode,
    string ErrorDetail)
{
    public bool IsConnected => State == HealthViewState.Connected;

    public static AgentHealthResult Connected(
        string agentStatus,
        string ipcStatus,
        int protocolVersion,
        bool hubConnectivityRequired,
        string correlationId) => new(
            HealthViewState.Connected,
            agentStatus,
            ipcStatus,
            protocolVersion,
            hubConnectivityRequired,
            correlationId,
            string.Empty,
            string.Empty);

    public static AgentHealthResult Failure(
        HealthViewState state,
        string errorCode,
        string errorDetail,
        string? correlationId = null,
        int? protocolVersion = null) => new(
            state,
            "Unavailable",
            "Unavailable",
            protocolVersion,
            null,
            correlationId,
            errorCode,
            errorDetail);
}
