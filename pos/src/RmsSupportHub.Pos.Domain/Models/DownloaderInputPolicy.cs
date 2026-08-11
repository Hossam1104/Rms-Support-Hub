using System.Text.RegularExpressions;

namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>Shared validation for values that influence downloader matching or path composition.</summary>
public static partial class DownloaderInputPolicy
{
    public const int MaxBranchCodeLength = 32;
    public const int MaxBranchesPerOperation = 64;

    public static IReadOnlyList<string> NormalizeBranchCodes(IReadOnlyList<string>? branchCodes)
    {
        if (branchCodes is null || branchCodes.Count == 0)
        {
            throw new ArgumentException("At least one branch code is required.", nameof(branchCodes));
        }

        if (branchCodes.Count > MaxBranchesPerOperation)
        {
            throw new ArgumentException("Too many branch codes were requested.", nameof(branchCodes));
        }

        var result = new List<string>(branchCodes.Count);
        foreach (var value in branchCodes)
        {
            var branch = value?.Trim() ?? string.Empty;
            if (!BranchCodeRegex().IsMatch(branch))
            {
                throw new ArgumentException("A branch code is invalid.", nameof(branchCodes));
            }

            if (!result.Contains(branch, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(branch);
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("At least one branch code is required.", nameof(branchCodes));
        }

        return result;
    }

    public static void ValidateSettings(DbDownloaderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.PollIntervalSeconds is < 1 or > 3600
            || settings.TimeoutSeconds is < 1 or > 86400
            || settings.StableSizeObservationAttempts is < 2 or > 10
            || settings.StableSizeObservationIntervalSeconds is < 1 or > 300)
        {
            throw new ArgumentException("Downloader timing settings are invalid.", nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.BackupRootFolder))
        {
            throw new ArgumentException("Downloader backup scope is invalid.", nameof(settings));
        }
    }

    public static bool IsSafeArchiveFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 255
        && value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        && value is not "." and not ".."
        && !value.Contains('/', StringComparison.Ordinal)
        && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains(':', StringComparison.Ordinal)
        && !value.Any(char.IsControl)
        && !value.Any(character => Path.GetInvalidFileNameChars().Contains(character));

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchCodeRegex();
}
