namespace RmsSupportHub.Pos.Contracts.V1.Artifacts;

/// <summary>
/// Metadata for a server-produced artifact (backup archive, downloaded branch ZIP, ...).
/// <see cref="ArtifactId"/> is opaque; the real storage path is never exposed (plan section 5.2).
/// </summary>
public sealed record ArtifactMetadataDto(
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null);
