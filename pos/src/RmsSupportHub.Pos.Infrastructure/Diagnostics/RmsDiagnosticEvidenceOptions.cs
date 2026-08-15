namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>Fixed, bounded sources used by the local failure analyzer.</summary>
public sealed class RmsDiagnosticEvidenceOptions
{
    public string BranchLogRoot { get; init; } = @"C:\ProgramData\Logs\Branch\BranchLogs";

    public string CashierLogRoot { get; init; } = @"C:\ProgramData\Logs\Cashier";

    public string ServicesManagerLogRoot { get; init; } = @"C:\ProgramData\Logs\Cashier";

    public int MaxLogFiles { get; init; } = 4;

    public int MaxBytesPerLog { get; init; } = 256 * 1024;

    public int MaxLinesPerLog { get; init; } = 256;

    public int MaxEventRecords { get; init; } = 64;

    public TimeSpan EventWindow { get; init; } = TimeSpan.FromHours(24);

    public void Validate()
    {
        if (MaxLogFiles is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(MaxLogFiles));
        if (MaxBytesPerLog is < 4096 or > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxBytesPerLog));
        if (MaxLinesPerLog is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaxLinesPerLog));
        if (MaxEventRecords is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(MaxEventRecords));
        if (EventWindow <= TimeSpan.Zero || EventWindow > TimeSpan.FromDays(7)) throw new ArgumentOutOfRangeException(nameof(EventWindow));
    }
}
