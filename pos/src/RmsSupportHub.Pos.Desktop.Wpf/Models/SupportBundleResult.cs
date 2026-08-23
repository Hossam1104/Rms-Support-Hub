using RmsSupportHub.Pos.Contracts.V1.Artifacts;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record SupportBundleResult(
    SupportBundleViewState State,
    ArtifactMetadataDto? Artifact,
    DateTimeOffset? CreatedAtUtc,
    string? CorrelationId,
    IReadOnlyList<string> IncludedSections,
    string ErrorCode,
    string ErrorDetail)
{
    public static SupportBundleResult Succeeded(
        ArtifactMetadataDto artifact,
        DateTimeOffset createdAtUtc,
        string correlationId,
        IReadOnlyList<string> includedSections) => new(
        SupportBundleViewState.Succeeded,
        artifact,
        createdAtUtc,
        correlationId,
        includedSections,
        string.Empty,
        string.Empty);

    public static SupportBundleResult Failure(
        SupportBundleViewState state,
        string errorCode,
        string errorDetail,
        string? correlationId = null) => new(
        state,
        null,
        null,
        correlationId,
        [],
        errorCode,
        errorDetail);
}
