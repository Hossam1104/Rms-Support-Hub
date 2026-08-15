using RmsSupportHub.Pos.Contracts.V1.Artifacts;

namespace RmsSupportHub.Pos.Contracts.V1.Support;

/// <summary>Opaque downloadable Support Bundle result produced from redacted typed evidence.</summary>
public sealed record SupportBundleDto(
    /// <summary>Principal-scoped artifact capability for the generated archive.</summary>
    ArtifactMetadataDto Artifact,
    /// <summary>UTC bundle creation time.</summary>
    DateTimeOffset CreatedAtUtc,
    /// <summary>Safe correlation identifier for the generation request.</summary>
    string CorrelationId,
    /// <summary>Server-owned sections included in the archive.</summary>
    IReadOnlyList<string> IncludedSections);
