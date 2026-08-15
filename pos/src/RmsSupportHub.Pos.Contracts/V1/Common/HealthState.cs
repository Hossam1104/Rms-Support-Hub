namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>Safe operator-facing health classification for a bounded diagnostic check.</summary>
public enum HealthState
{
    Healthy,
    Warning,
    ActionRequired,
    Unknown
}
