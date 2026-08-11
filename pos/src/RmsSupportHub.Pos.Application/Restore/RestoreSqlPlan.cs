using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Restore;

/// <summary>Testable SQL logical-file to server-owned physical-file mapping seam.</summary>
public interface IRestoreSqlPlanBuilder
{
    RestoreSqlPlan Build(
        string targetDatabase,
        string dbFilesPath,
        IReadOnlyList<RestoreFileInfo> logicalFiles);
}

public sealed partial class RestoreSqlPlanBuilder : IRestoreSqlPlanBuilder
{
    private const int MaxLogicalFiles = 32;

    public RestoreSqlPlan Build(
        string targetDatabase,
        string dbFilesPath,
        IReadOnlyList<RestoreFileInfo> logicalFiles)
    {
        if (string.IsNullOrWhiteSpace(targetDatabase) || !SafeIdentifierRegex().IsMatch(targetDatabase))
        {
            throw new RestorePolicyException("restore.sql_plan_invalid", "The target database is not supported.");
        }

        if (string.IsNullOrWhiteSpace(dbFilesPath) || !Path.IsPathRooted(dbFilesPath))
        {
            throw new RestorePolicyException("restore.destination_unsafe", "The database file destination is not supported.");
        }

        if (dbFilesPath.Contains('%') || dbFilesPath.Contains('\0')
            || dbFilesPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is ".." or "."))
        {
            throw new RestorePolicyException("restore.destination_unsafe", "The database file destination is not supported.");
        }

        if (logicalFiles is null || logicalFiles.Count == 0 || logicalFiles.Count > MaxLogicalFiles)
        {
            throw new RestorePolicyException("restore.sql_plan_invalid", "The archive did not provide a bounded usable SQL file list.");
        }

        var canonicalRoot = Path.GetFullPath(dbFilesPath);
        var rootPrefix = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            || canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;
        var moves = new List<RestoreMovePlan>(logicalFiles.Count);
        var logicalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataIndex = 0;
        var logIndex = 0;

        foreach (var logicalFile in logicalFiles)
        {
            var logicalName = logicalFile.LogicalName?.Trim() ?? string.Empty;
            var fileType = logicalFile.FileType?.Trim().ToUpperInvariant() ?? string.Empty;
            if (logicalName.Length == 0 || logicalName.Length > 128
                || logicalName.Contains('\0')
                || logicalName.Contains('/')
                || logicalName.Contains('\\')
                || !logicalNames.Add(logicalName)
                || fileType is not ("D" or "L"))
            {
                throw new RestorePolicyException("restore.sql_plan_invalid", "The SQL logical-file list is not supported.");
            }

            var destinationFileName = fileType == "D"
                ? dataIndex++ == 0
                    ? $"{targetDatabase}.mdf"
                    : $"{targetDatabase}_{dataIndex}.ndf"
                : logIndex++ == 0
                    ? $"{targetDatabase}_log.ldf"
                    : $"{targetDatabase}_log_{logIndex}.ldf";
            var destinationPath = Path.GetFullPath(Path.Combine(canonicalRoot, destinationFileName));
            var isWithinRoot = destinationPath.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
                || destinationPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
            if (!isWithinRoot)
            {
                throw new RestorePolicyException("restore.destination_unsafe", "The database file destination is not supported.");
            }

            moves.Add(new RestoreMovePlan(logicalName, fileType, destinationFileName, destinationPath));
        }

        if (dataIndex == 0 || logIndex == 0)
        {
            throw new RestorePolicyException("restore.sql_plan_invalid", "The SQL logical-file list must contain data and log files.");
        }

        return new RestoreSqlPlan(targetDatabase, canonicalRoot, moves);
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafeIdentifierRegex();
}

public sealed record RestoreSqlPlan(
    string TargetDatabase,
    string DbFilesPath,
    IReadOnlyList<RestoreMovePlan> Moves);

public sealed record RestoreMovePlan(
    string LogicalName,
    string FileType,
    string DestinationFileName,
    string DestinationPath);

public sealed class RestorePolicyException(string code, string safeMessage) : InvalidOperationException(safeMessage)
{
    public string Code { get; } = code;

    public string SafeMessage { get; } = safeMessage;
}
