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

    public void Validate()
    {
        ValidateRoot(BackupRootPath, nameof(BackupRootPath));
        ValidateRoot(DatabaseFilesRootPath, nameof(DatabaseFilesRootPath));
        if (MaximumBackupsPerDatabase is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBackupsPerDatabase));
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
