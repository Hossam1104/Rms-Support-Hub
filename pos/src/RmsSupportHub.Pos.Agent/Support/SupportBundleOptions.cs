namespace RmsSupportHub.Pos.Agent.Support;

/// <summary>Fixed Agent-owned root and size policy for redacted Support Bundle archives.</summary>
public sealed class SupportBundleOptions
{
    public string BundleRootPath { get; init; } = @"C:\ProgramData\RMS_Plus\SupportHub\Bundles";

    public long MaximumBundleBytes { get; init; } = 8L * 1024 * 1024;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BundleRootPath)
            || !Path.IsPathRooted(BundleRootPath)
            || BundleRootPath.Contains('\0')
            || BundleRootPath.Contains('%')
            || BundleRootPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("A bounded absolute bundle root is required.", nameof(BundleRootPath));
        }

        if (MaximumBundleBytes is < 64 * 1024 or > 64L * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBundleBytes));
        }
    }
}
