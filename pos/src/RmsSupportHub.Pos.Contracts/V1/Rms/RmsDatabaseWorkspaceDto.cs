namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsDatabaseWorkspaceDto(
    RmsDatabaseTarget Target,
    string DatabaseDisplayName,
    string RestoreConfirmationText,
    IReadOnlyList<RmsDatabaseArtifactDto> ApprovedBackups,
    RmsDatabaseOperationDto? LatestOperation);
