using System.Text.RegularExpressions;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>Removes secret-bearing and host-specific data before evidence crosses the Agent seam.</summary>
public static partial class DiagnosticRedactor
{
    private const int MaxSummaryLength = 512;
    private const int MaxFrameLength = 256;

    public static string RedactSummary(string? value)
    {
        var text = DiagnosticSecretRedactionPipeline.Redact(value, MaxSummaryLength);
        text = UrlSecretPattern().Replace(text, "$1[redacted]");
        text = WindowsPathPattern().Replace(text, "[redacted path]");
        text = UserIdentityPattern().Replace(text, "[redacted user]");
        text = SidPattern().Replace(text, "[redacted SID]");
        text = new string(text.Where(character => !char.IsControl(character) || character is '\t').ToArray()).Trim();
        return text.Length <= MaxSummaryLength ? text : text[..MaxSummaryLength];
    }

    public static string? ExceptionType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = ExceptionTypePattern().Match(value);
        return match.Success ? match.Groups[1].Value[..Math.Min(128, match.Groups[1].Value.Length)] : null;
    }

    public static IReadOnlyList<string> StackFrames(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(value => value.Trim())
            .Where(value => value.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
            .Select(value => RedactSummary(value))
            .Where(value => value.Length > 3)
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .Select(value => value.Length <= MaxFrameLength ? value : value[..MaxFrameLength])
            .ToArray();

    [GeneratedRegex(@"(?i)(https?://[^\s?]+\?[^\s]*?)(?:password|pwd|secret|token|key)=[^&\s]+")]
    private static partial Regex UrlSecretPattern();

    [GeneratedRegex(@"(?i)(?:[A-Z]:\\|\\\\)[^\s""']+")]
    private static partial Regex WindowsPathPattern();

    [GeneratedRegex(@"(?i)(?:domain\\)?[a-z0-9_.-]+\\[a-z0-9_.-]+")]
    private static partial Regex UserIdentityPattern();

    [GeneratedRegex(@"(?i)\bS-1-(?:\d+-){1,14}\d+\b")]
    private static partial Regex SidPattern();

    [GeneratedRegex(@"(?i)\b([A-Za-z_][A-Za-z0-9_.]*(?:Exception|Error))\b")]
    private static partial Regex ExceptionTypePattern();

}
