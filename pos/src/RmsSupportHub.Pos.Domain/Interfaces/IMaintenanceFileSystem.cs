namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// Filesystem seam for maintenance policy and execution.  Production code supplies a physical
/// adapter; tests supply a disposable fake and therefore never delete a real device path.
/// </summary>
public interface IMaintenanceFileSystem
{
    string ExpandEnvironmentVariables(string path);

    string GetFullPath(string path);

    MaintenancePathInspection Inspect(string path);

    IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path);

    long? TryGetAvailableFreeSpace(string path);

    Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default);
}

public sealed record MaintenancePathInspection(
    string Path,
    bool Exists,
    bool IsDirectory,
    bool IsReparsePoint,
    string? ResolvedLinkTarget,
    long? LengthBytes,
    int? ChildCount,
    bool InspectionFailed = false);
