using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum ServiceHealthRowState
{
    Unknown,
    Running,
    Stopped,
    Paused,
    Transitioning,
    NotInstalled
}

public sealed record ServiceHealthRow(
    string DisplayName,
    bool Required,
    bool Installed,
    ServiceHealthRowState State,
    string SafeStatusCode)
{
    public string RuntimeLabel => State switch
    {
        ServiceHealthRowState.Running => "Running",
        ServiceHealthRowState.Stopped => "Stopped",
        ServiceHealthRowState.Paused => "Paused",
        ServiceHealthRowState.Transitioning => "Transitioning",
        ServiceHealthRowState.NotInstalled => "Not installed",
        _ => "Unable to determine"
    };

    public string InstallationLabel => Installed ? "Installed" : "Not installed";

    public string RequirementLabel => Required ? "Required" : "Optional";

    public string StatusGlyph => State switch
    {
        ServiceHealthRowState.Running => "●",
        ServiceHealthRowState.Stopped or ServiceHealthRowState.Paused => "▲",
        ServiceHealthRowState.Transitioning => "◌",
        ServiceHealthRowState.NotInstalled => "—",
        _ => "?"
    };

    public static bool TryCreate(
        ServiceHealthItemDto item,
        out ServiceHealthRow? row)
    {
        row = null;
        if (string.IsNullOrWhiteSpace(item.DisplayName)
            || string.IsNullOrWhiteSpace(item.SafeStatusCode))
        {
            return false;
        }

        var state = item.RuntimeState switch
        {
            ServiceRuntimeState.Running => ServiceHealthRowState.Running,
            ServiceRuntimeState.Stopped => ServiceHealthRowState.Stopped,
            ServiceRuntimeState.Paused => ServiceHealthRowState.Paused,
            ServiceRuntimeState.Transitioning => ServiceHealthRowState.Transitioning,
            ServiceRuntimeState.NotFound => ServiceHealthRowState.NotInstalled,
            ServiceRuntimeState.Unknown => ServiceHealthRowState.Unknown,
            _ => (ServiceHealthRowState?)null
        };
        if (state is null)
        {
            return false;
        }

        row = new(
            item.DisplayName,
            item.Required,
            item.Installed,
            state.Value,
            item.SafeStatusCode);
        return true;
    }
}
