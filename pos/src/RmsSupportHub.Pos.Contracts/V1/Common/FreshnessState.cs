namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// How current a piece of polled evidence is. Plan section 7.3 requires this be shown explicitly
/// and never collapsed into a single "online" boolean.
/// </summary>
public enum FreshnessState
{
    Unknown,
    Fresh,
    Stale,
}
