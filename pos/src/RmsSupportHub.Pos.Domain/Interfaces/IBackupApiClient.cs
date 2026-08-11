namespace RmsSupportHub.Pos.Domain.Interfaces;

using RmsSupportHub.Pos.Domain.Models;

public interface IBackupApiClient
{
    Task<DownloaderTriggerResult> TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default);
}
