using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

public sealed partial class DiagnosticOutputRedactor : IDiagnosticConsoleOutputRedactor
{
    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var redacted = DiagnosticSecretRedactionPipeline.Redact(value, 64 * 1024);
        redacted = WindowsPathRegex().Replace(redacted, "<path>");
        redacted = UncPathRegex().Replace(redacted, "<network-path>");
        redacted = SidRegex().Replace(redacted, "<sid>");
        return redacted.Length <= 64 * 1024 ? redacted : redacted[..(64 * 1024)];
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:[A-Za-z]:\\|[A-Za-z]:/)[^\r\n\t ]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"\\\\[^\r\n\t ]+", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"(?i)S-\d-\d+(?:-\d+){1,14}", RegexOptions.CultureInvariant)]
    private static partial Regex SidRegex();

}
