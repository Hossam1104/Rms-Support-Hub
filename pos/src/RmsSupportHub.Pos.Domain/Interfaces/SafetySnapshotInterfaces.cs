using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface ISafetySnapshotFactory
{
    Task<SafetySnapshotDocument> CaptureAsync(
        string principalSid,
        string environment,
        string? profileId,
        string correlationId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);
}
