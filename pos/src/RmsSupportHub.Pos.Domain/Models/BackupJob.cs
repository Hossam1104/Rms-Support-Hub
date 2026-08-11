namespace RmsSupportHub.Pos.Domain.Models;

public sealed class BackupJob(IReadOnlyList<string> requestedBranchCodes)
{
    public IReadOnlyList<string> RequestedBranchCodes { get; } = requestedBranchCodes;

    public DateTimeOffset TriggeredAtUtc { get; } = DateTimeOffset.UtcNow;

    public BackupJob(IReadOnlyList<string> requestedBranchCodes, TimeProvider timeProvider)
        : this(requestedBranchCodes)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TriggeredAtUtc = timeProvider.GetUtcNow();
    }

    public string? BatchFolderPath { get; set; }

    public string? Serial { get; set; }

    public List<BranchBackupItem> Items { get; } =
        [.. requestedBranchCodes.Select(code => new BranchBackupItem(code))];

    public bool IsComplete => Items.All(item => item.Status
        is Enums.BranchBackupStatus.Downloaded
        or Enums.BranchBackupStatus.Failed
        or Enums.BranchBackupStatus.TimedOut
        or Enums.BranchBackupStatus.Cancelled);
}
