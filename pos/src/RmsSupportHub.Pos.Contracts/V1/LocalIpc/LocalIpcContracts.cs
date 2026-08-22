using System.Text.Json;

namespace RmsSupportHub.Pos.Contracts.V1.LocalIpc;

public static class LocalIpcProtocol
{
    public const int CurrentVersion = 1;

    public const string PipeName = "RmsSupportAgent.Ipc";

    public const string HealthOperation = "agent.health";

    public const string InstallationDiscoveryOperation = "rms.installation.discovery";
}

public sealed record LocalIpcRequestEnvelope(
    int ProtocolVersion,
    string RequestId,
    string? CorrelationId,
    string Operation,
    JsonElement? Payload);

public sealed record LocalIpcResponseEnvelope(
    int ProtocolVersion,
    string RequestId,
    string CorrelationId,
    bool Success,
    JsonElement? Result,
    LocalIpcErrorDto? Error);

public sealed record LocalIpcErrorDto(string Code, string Message);

/// <summary>Safe local Agent/IPC readiness information. Hub connectivity is deliberately absent.</summary>
public sealed record LocalIpcHealthDto(
    string AgentStatus,
    string IpcStatus,
    int ProtocolVersion,
    bool HubConnectivityRequired);
