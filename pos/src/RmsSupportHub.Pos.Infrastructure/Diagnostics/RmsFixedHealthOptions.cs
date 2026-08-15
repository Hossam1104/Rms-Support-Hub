using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// Server-owned RMS evidence roots. This descriptor list is the only filesystem scope accepted by
/// the fixed health reader; it is not replaced by request data.
/// </summary>
public sealed class RmsFixedHealthOptions
{
    public string SetupRoot { get; init; } = @"C:\ProgramData\RMS_Plus";

    public string DownloadsRoot { get; init; } = @"C:\ProgramData\RMS_Plus_Downloads";

    public string ReleaseRepositoryRoot { get; init; } = @"C:\ProgramData\RMS_Plus_ReleaseRepo";

    public string BranchRoot { get; init; } = @"C:\ProgramData\Branch";

    public string CashierRoot { get; init; } = @"C:\ProgramData\Cashier";

    public string BranchLogRoot { get; init; } = @"C:\ProgramData\Logs\Branch\BranchLogs";

    public string CashierLogRoot { get; init; } = @"C:\ProgramData\Logs\Cashier";

    public string InsuranceAttachmentRoot { get; init; } = @"C:\ProgramData\DBS\POS";

    public int MaximumFilesPerRoot { get; init; } = 10_000;

    public long MaximumBytesPerRoot { get; init; } = 4L * 1024 * 1024 * 1024;

    public IReadOnlyList<RmsFixedRootDefinition> Definitions =>
    [
        new("rms-setup", "RMS setup and release", SetupRoot, false),
        new("rms-downloads", "RMS downloads", DownloadsRoot, false),
        new("rms-release-repository", "RMS release repository", ReleaseRepositoryRoot, false),
        new("branch-data", "Branch data", BranchRoot, false),
        new("cashier-data", "Cashier data", CashierRoot, false),
        new("branch-logs", "Branch logs", BranchLogRoot, false),
        new("cashier-logs", "Cashier logs", CashierLogRoot, false),
        new("insurance-attachments", "Insurance attachment storage", InsuranceAttachmentRoot, true)
    ];

    public void Validate()
    {
        foreach (var definition in Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.RootPath)
                || !Path.IsPathFullyQualified(definition.RootPath)
                || definition.RootPath.Any(char.IsControl)
                || definition.RootPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                    .Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException($"The fixed RMS root '{definition.RootId}' is invalid.");
            }
        }

        if (MaximumFilesPerRoot is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaximumFilesPerRoot));
        if (MaximumBytesPerRoot is < 1 or > 64L * 1024 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumBytesPerRoot));
    }
}
