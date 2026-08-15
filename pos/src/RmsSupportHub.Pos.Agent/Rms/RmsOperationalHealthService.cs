using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>Projects fixed-root RMS evidence into a safe transport contract.</summary>
public sealed class RmsOperationalHealthService(
    IRmsFixedHealthReader reader,
    TimeProvider timeProvider)
{
    public async Task<RmsOperationalHealthDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var rootsTask = reader.ReadRootsAsync(cancellationToken);
        var updateTask = reader.ReadUpdateHealthAsync(cancellationToken);
        var attachmentsTask = reader.ReadInsuranceAttachmentHealthAsync(cancellationToken);
        await Task.WhenAll(rootsTask, updateTask, attachmentsTask).ConfigureAwait(false);
        return new(
            rootsTask.Result.Select(ToDto).ToArray(),
            ToDto(updateTask.Result),
            ToDto(attachmentsTask.Result),
            timeProvider.GetUtcNow());
    }

    private static RmsFixedRootHealthDto ToDto(RmsFixedRootHealth value) => new(
        value.RootId,
        value.DisplayName,
        Enum.TryParse<RmsFixedRootStateDto>(value.State.ToString(), out var state) ? state : RmsFixedRootStateDto.Unknown,
        value.Exists,
        value.Accessible,
        value.FileCount,
        value.TotalBytes,
        value.OldestFileUtc,
        value.NewestFileUtc,
        value.FreeBytes,
        value.TotalCapacityBytes,
        value.Detail);

    private static RmsUpdateHealthDto ToDto(RmsUpdateHealth value) => new(
        ToDto(value.SetupRoot),
        ToDto(value.DownloadsRoot),
        ToDto(value.ReleaseRepositoryRoot),
        value.ProductRelease,
        value.ReleaseFileAvailable,
        value.PackageState,
        value.Detail);

    private static RmsInsuranceAttachmentHealthDto ToDto(RmsInsuranceAttachmentHealth value) => new(
        ToDto(value.Root),
        value.AttachmentCount,
        value.TotalBytes,
        value.OldestAttachmentUtc,
        value.NewestAttachmentUtc,
        value.Detail);
}
