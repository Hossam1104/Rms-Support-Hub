using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record LogEvidenceRecordRow(
    string Source,
    DateTimeOffset? AtUtc,
    string Summary,
    string? ExceptionType,
    IReadOnlyList<string> StackFrames,
    string? EventId)
{
    public string AtDisplay => AtUtc is { } value
        ? value.ToLocalTime().ToString("HH:mm:ss")
        : "Time unavailable";

    public string ExceptionTypeDisplay => ExceptionType ?? "No exception type";

    public string EventIdDisplay => EventId ?? "No event ID";

    public string StackFramesDisplay => StackFrames.Count == 0
        ? "No stack frames reported"
        : string.Join("  >  ", StackFrames);

    public static bool TryCreate(
        FailureEvidenceDto item,
        out LogEvidenceRecordRow? row)
    {
        row = null;
        if (!IsSafeText(item.Source, 128)
            || item.AtUtc is { } atUtc && atUtc == default
            || !IsSafeText(item.Summary, 512)
            || !IsSafeText(item.ExceptionType, 128)
            || item.StackFrames is null
            || item.StackFrames.Count > 12
            || item.StackFrames.Any(frame => !IsSafeText(frame, 256))
            || !IsSafeText(item.EventId, 64))
        {
            return false;
        }

        row = new(
            item.Source,
            item.AtUtc,
            item.Summary,
            item.ExceptionType,
            item.StackFrames.ToArray(),
            item.EventId);
        return true;
    }

    private static bool IsSafeText(string? value, int maximumLength)
    {
        if (value is null)
        {
            return true;
        }

        return value.Length <= maximumLength
            && !value.Any(char.IsControl)
            && !value.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }
}
