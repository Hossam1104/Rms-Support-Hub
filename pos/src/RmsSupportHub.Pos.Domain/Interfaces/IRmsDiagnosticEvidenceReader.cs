namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Safe severity assigned to an individual bounded diagnostic evidence record.</summary>
public enum RmsEvidenceSeverity
{
    Informational,
    Warning,
    Error
}

/// <summary>One already-bounded and redacted local evidence record.</summary>
public sealed record RmsDiagnosticEvidenceRecord(
    string Source,
    DateTimeOffset? AtUtc,
    RmsEvidenceSeverity Severity,
    string Summary,
    string? ExceptionType,
    IReadOnlyList<string> StackFrames,
    string? EventId);

/// <summary>Bounded evidence read result, including safe reasons for unavailable sources.</summary>
public sealed record RmsDiagnosticEvidenceReadResult(
    IReadOnlyList<RmsDiagnosticEvidenceRecord> Records,
    IReadOnlyList<string> UnknownReasons);

/// <summary>
/// Server-owned diagnostic evidence port. Implementations may read only fixed RMS log roots and
/// allow-listed Windows event identifiers; callers never provide a path, provider, query, or event ID.
/// </summary>
public interface IRmsDiagnosticEvidenceReader
{
    Task<RmsDiagnosticEvidenceReadResult> ReadAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}
