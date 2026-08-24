using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record BackupArtifactRow(
    RmsDatabaseTarget Target,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    LocalIpcBackupAvailability Availability)
{
    public string TargetLabel => Target switch
    {
        RmsDatabaseTarget.Branch => "Branch",
        RmsDatabaseTarget.Cashier => "Cashier",
        _ => "Unknown"
    };

    public string AvailabilityLabel => Availability switch
    {
        LocalIpcBackupAvailability.Available => "Verified",
        LocalIpcBackupAvailability.Expired => "Expired",
        LocalIpcBackupAvailability.Missing => "Unavailable",
        LocalIpcBackupAvailability.ChecksumMismatch => "Checksum mismatch",
        _ => "Invalid"
    };

    public string SizeDisplay => $"{SizeBytes:N0} bytes";

    public string CreatedDisplay => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string ExpiryDisplay => ExpiresAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public bool CanExport => Availability == LocalIpcBackupAvailability.Available;

    public static bool TryCreate(LocalIpcBackupArtifactDto? item, out BackupArtifactRow? row)
    {
        row = null;
        if (item is null
            || !Enum.IsDefined(item.Target)
            || !Enum.IsDefined(item.Availability)
            || item.ArtifactId.Length != 32
            || !item.ArtifactId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            || string.IsNullOrWhiteSpace(item.DisplayName)
            || item.DisplayName.Length > 128
            || item.DisplayName.Any(char.IsControl)
            || item.DisplayName.Contains('/')
            || item.DisplayName.Contains('\\')
            || item.SizeBytes <= 0
            || item.SizeBytes > 512L * 1024 * 1024
            || item.CreatedAtUtc == default
            || item.ExpiresAtUtc <= item.CreatedAtUtc
            || item.Sha256Checksum.Length != 64
            || !item.Sha256Checksum.All(Uri.IsHexDigit))
        {
            return false;
        }

        row = new(
            item.Target,
            item.ArtifactId,
            item.DisplayName,
            item.SizeBytes,
            item.Sha256Checksum,
            item.CreatedAtUtc,
            item.ExpiresAtUtc,
            item.Availability);
        return true;
    }
}

public sealed record BackupInventoryResult(
    BackupViewState State,
    IReadOnlyList<BackupArtifactRow> Items,
    DateTimeOffset? CheckedAtUtc,
    string ErrorCode,
    string ErrorDetail,
    bool CanRead,
    bool CanCreate,
    bool CanExport)
{
    public static BackupInventoryResult Failure(
        BackupViewState state,
        string code,
        string detail) => new(state, [], null, code, detail, false, false, false);
}

public sealed record BackupOperationResult(
    BackupViewState State,
    RmsDatabaseOperationDto? Operation,
    string ErrorCode,
    string ErrorDetail)
{
    public static BackupOperationResult Failure(
        BackupViewState state,
        string code,
        string detail) => new(state, null, code, detail);
}

public sealed record ArtifactExportResult(
    ArtifactExportViewState State,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    string DestinationCategory,
    string ErrorCode,
    string ErrorDetail)
{
    public static ArtifactExportResult Failure(
        ArtifactExportViewState state,
        string code,
        string detail,
        string? artifactId = null) => new(state, artifactId ?? string.Empty, string.Empty, 0, string.Empty, string.Empty, code, detail);
}
