namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsDatabaseArtifactDto(
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
