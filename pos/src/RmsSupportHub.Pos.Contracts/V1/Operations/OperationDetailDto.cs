using RmsSupportHub.Pos.Contracts.V1.Maintenance;
using RmsSupportHub.Pos.Contracts.V1.Downloader;

namespace RmsSupportHub.Pos.Contracts.V1.Operations;

/// <summary>
/// <c>GET /api/v1/operations/{id}</c> response — the read-only rehydration path after a browser
/// refresh or a dropped SSE stream (plan section 5.3). <see cref="ResultArtifactIds"/> are opaque;
/// no raw file-system path is ever included.
/// </summary>
public sealed record OperationDetailDto(
    string OperationId,
    string OperationType,
    OperationState State,
    int ProgressPercent,
    string CurrentStage,
    string BranchCodeSnapshot,
    string RequestingPrincipal,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    IReadOnlyList<string> OwnedResourceLocks,
    IReadOnlyList<OperationEventDto> Events,
    IReadOnlyList<string> ResultArtifactIds,
    string? ErrorCode,
    string? CorrelationId,
    string? ResolvedDestinationReference = null,
    MaintenanceOperationOutcomeDto? MaintenanceOutcome = null,
    DownloaderOperationOutcomeDto? DownloaderOutcome = null);
