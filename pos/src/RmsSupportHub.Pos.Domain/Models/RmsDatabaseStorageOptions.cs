namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Fixed Agent-owned storage roots. They are configuration for the privileged process only and
/// are never accepted from or returned to a browser.
/// </summary>
public sealed class RmsDatabaseStorageOptions
{
    public string BackupRootPath { get; init; } = @"C:\ProgramData\RMS_Plus\SupportHub\Backups";

    public string DatabaseFilesRootPath { get; init; } = @"C:\ProgramData\RMS_Plus\SupportHub\DatabaseFiles";

    public int MaximumBackupsPerDatabase { get; init; } = 32;

    /// <summary>
    /// Maximum age of a physical database backup before the durable backup catalog prunes it. This
    /// is a dedicated policy for physical RMS database backups and is intentionally independent of
    /// the generic in-memory <see cref="RuntimeRetentionPolicy.ArtifactLifetime"/> browser-download
    /// lifetime: a backup must never disappear merely because a download capability expired. 30 days
    /// is a conservative default for local recovery -- long enough to cover a missed maintenance
    /// window, bounded so local disk usage cannot grow without limit.
    /// </summary>
    public TimeSpan BackupRetention { get; init; } = TimeSpan.FromDays(30);

    public void Validate()
    {
        ValidateRoot(BackupRootPath, nameof(BackupRootPath));
        ValidateRoot(DatabaseFilesRootPath, nameof(DatabaseFilesRootPath));
        if (MaximumBackupsPerDatabase is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBackupsPerDatabase));
        }

        if (BackupRetention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(BackupRetention));
        }
    }

    private static void ValidateRoot(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Path.IsPathRooted(value)
            || value.Contains('\0')
            || value.Contains('%')
            || value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("A bounded absolute storage root is required.", parameterName);
        }

        var fullPath = Path.GetFullPath(value);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root)
            || string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A storage root cannot be a filesystem volume root.", parameterName);
        }
    }
}
