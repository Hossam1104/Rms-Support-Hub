using System.Text.RegularExpressions;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// The single secret scrubber used at every diagnostic evidence boundary. It deliberately
/// redacts the value rather than trying to preserve the source format: malformed, quoted, and
/// truncated input must fail closed just like well-formed input.
/// </summary>
internal static partial class DiagnosticSecretRedactionPipeline
{
    public static string Redact(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0) return string.Empty;

        var redacted = value.Length > maxLength ? value[..maxLength] : value;
        redacted = PrivateKeyPattern().Replace(redacted, "<key-material>");
        redacted = XmlKeyValueSecretPattern().Replace(redacted, static match =>
            match.Groups["prefix"].Value + match.Groups["quote"].Value + "<redacted>" + match.Groups["quote"].Value);
        redacted = XmlSecretAttributePattern().Replace(redacted, static match =>
            match.Groups["prefix"].Value + match.Groups["quote"].Value + "<redacted>" + match.Groups["quote"].Value);
        redacted = JsonSecretPattern().Replace(redacted, static match => match.Groups["prefix"].Value + "<redacted>");
        redacted = ConnectionStringSecretPattern().Replace(redacted, static match => match.Groups["prefix"].Value + "<redacted>");
        redacted = BearerPattern().Replace(redacted, "$1<redacted>");
        redacted = SecretAssignmentPattern().Replace(redacted, static match => match.Groups["prefix"].Value + "<redacted>");
        redacted = UrlSecretPattern().Replace(redacted, "$1<redacted>");
        return redacted.Length <= maxLength ? redacted : redacted[..maxLength];
    }

    // The end marker is optional on purpose. A process killed while emitting a PEM must not leave
    // the beginning of a private key in an artifact, timeline entry, or support bundle.
    [GeneratedRegex(@"-----BEGIN[^\r\n-]*PRIVATE KEY-----[\s\S]*?(?:-----END[^\r\n-]*PRIVATE KEY-----|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"(?is)(?<prefix><[^>]*?\b(?:key|name)\s*=\s*[""'][^""']*(?:password|passwd|pwd|secret|token|api[-_]?key|connectionstring|clientsecret|accessToken|private[-_ ]?key|certificate|dpapi|pfx)[^""']*[""'][^>]*?\b(?:value|connectionString)\s*=\s*)(?<quote>[""'])(?<value>.*?)(?:\k<quote>|(?=[\s/>]|$))", RegexOptions.CultureInvariant)]
    private static partial Regex XmlKeyValueSecretPattern();

    // Covers configuration XML, including <add key="Password" value="..."> and
    // connectionStrings entries. The value's closing quote is optional for truncated output.
    [GeneratedRegex(@"(?is)(?<prefix><[^>]*?\b(?:password|passwd|pwd|secret|token|api[-_]?key|connectionstring|clientsecret|accessToken|private[-_ ]?key|certificate|dpapi|pfx)\s*=\s*)(?<quote>[""'])(?<value>.*?)(?:\k<quote>|(?=[\s/>]|$))", RegexOptions.CultureInvariant)]
    private static partial Regex XmlSecretAttributePattern();

    // Covers JSON string values and an unterminated value at the end of a bounded capture.
    [GeneratedRegex(@"(?is)(?<prefix>[""'](?:password|passwd|pwd|secret|token|api[-_]?key|connectionstring|clientsecret|access_token|accessToken|authorization|private[-_ ]?key|certificate|dpapi|pfx)[""']\s*:\s*)(?<value>[""'](?:\\.|[^""'])*[""']|[^,}\r\n]*)", RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretPattern();

    [GeneratedRegex(@"(?i)(?<prefix>\b(?:data source|server|initial catalog|user id|uid|user|password|pwd|access token|token)\s*=\s*)(?<value>""[^""]*""|'[^']*'|[^;,\r\n]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringSecretPattern();

    [GeneratedRegex(@"(?i)(?<prefix>\b(?:password|passwd|pwd|secret|token|api[-_]?key|connectionstring|clientsecret|credential|authorization|private[-_ ]?key|certificate|dpapi|pfx)\b\s*(?:=|:|=>)\s*)(?<value>""[^""]*""|'[^']*'|[^\s,;}\r\n]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"(?i)((?:authorization\s*[:=]\s*)?bearer\s+)[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"(?i)(https?://[^\s?]+\?[^\s]*?(?:password|pwd|secret|token|key)=)[^&\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlSecretPattern();
}
