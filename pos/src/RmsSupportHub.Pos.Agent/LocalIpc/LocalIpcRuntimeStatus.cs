using RmsSupportHub.Pos.Contracts.V1.LocalIpc;

namespace RmsSupportHub.Pos.Agent.LocalIpc;

public sealed class LocalIpcRuntimeStatus
{
    private readonly object gate = new();
    private string state = "disabled";
    private string? reason;

    public void SetDisabled() => Set("disabled", null);

    public void SetStarting() => Set("starting", null);

    public void SetListening() => Set("ready", null);

    public void SetUnavailable(string unavailableReason) => Set("unavailable", unavailableReason);

    public LocalIpcHealthDto GetHealth()
    {
        lock (gate)
        {
            return new("ready", state, LocalIpcProtocol.CurrentVersion, HubConnectivityRequired: false);
        }
    }

    public string? GetReason()
    {
        lock (gate)
        {
            return reason;
        }
    }

    private void Set(string nextState, string? nextReason)
    {
        lock (gate)
        {
            state = nextState;
            reason = nextReason;
        }
    }
}
