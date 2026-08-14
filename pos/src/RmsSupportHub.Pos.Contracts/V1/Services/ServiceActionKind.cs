namespace RmsSupportHub.Pos.Contracts.V1.Services;

/// <summary>
/// Start/stop/restart only — matches current WinUI scope (plan section 3.2). Deliberately excludes
/// service deletion; do not add it here without a plan change and the full destructive-operation
/// control set from plan section 6.3.
/// </summary>
public enum ServiceActionKind
{
    Start,
    Stop,
    Restart,
}
