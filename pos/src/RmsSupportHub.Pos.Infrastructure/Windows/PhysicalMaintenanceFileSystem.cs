using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

/// <summary>Physical maintenance adapter. It is registered only at the privileged composition roots.</summary>
public sealed class PhysicalMaintenanceFileSystem : IMaintenanceFileSystem
{
    public string ExpandEnvironmentVariables(string path) => Environment.ExpandEnvironmentVariables(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public MaintenancePathInspection Inspect(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var directory = new DirectoryInfo(path);
                var attributes = directory.Attributes;
                var linkTarget = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
                var childCount = 0;
                try
                {
                    childCount = directory.EnumerateFileSystemInfos().Take(10_001).Count();
                }
                catch
                {
                    return new(path, true, true, (attributes & FileAttributes.ReparsePoint) != 0, linkTarget, null, null, true);
                }

                return new(
                    path,
                    true,
                    true,
                    (attributes & FileAttributes.ReparsePoint) != 0,
                    linkTarget,
                    null,
                    childCount > 10_000 ? 10_001 : childCount);
            }

            if (File.Exists(path))
            {
                var file = new FileInfo(path);
                var attributes = file.Attributes;
                return new(
                    path,
                    true,
                    false,
                    (attributes & FileAttributes.ReparsePoint) != 0,
                    file.ResolveLinkTarget(returnFinalTarget: true)?.FullName,
                    file.Length,
                    null);
            }

            return new(path, false, false, false, null, null, null);
        }
        catch (FileNotFoundException)
        {
            return new(path, false, false, false, null, null, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new(path, false, false, false, null, null, null);
        }
        catch
        {
            return new(path, false, false, false, null, null, null, true);
        }
    }

    public IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path)
    {
        try
        {
            var parts = new Stack<string>();
            var current = path;
            while (!string.IsNullOrWhiteSpace(current))
            {
                parts.Push(current);
                var parent = Directory.GetParent(current)?.FullName;
                if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
                current = parent ?? string.Empty;
            }

            var inspections = new List<MaintenancePathInspection>();
            while (parts.Count > 0)
            {
                var candidate = parts.Pop();
                var inspection = Inspect(candidate);
                if (!inspection.Exists && !inspection.InspectionFailed) break;
                inspections.Add(inspection);
                if (inspection.InspectionFailed) break;
            }

            return inspections;
        }
        catch
        {
            return [new(path, false, false, false, null, null, null, true)];
        }
    }

    public long? TryGetAvailableFreeSpace(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root)) return null;
        return new DriveInfo(root).AvailableFreeSpace;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recursive) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }, CancellationToken.None);
    }
}
