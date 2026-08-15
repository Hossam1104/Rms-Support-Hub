namespace RmsSupportHub.Pos.Infrastructure.Audit;

/// <summary>Fixed, service-owned durable audit storage. No caller-provided path is accepted.</summary>
public sealed class AgentAuditOptions
{
    public string RootPath { get; init; } = @"C:\ProgramData\DBS\RmsSupportAgent\Audit";

    public int MaximumEntries { get; init; } = 512;

    public long MaximumBytes { get; init; } = 8L * 1024 * 1024;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath)
            || !Path.IsPathFullyQualified(RootPath)
            || RootPath.Any(char.IsControl)
            || RootPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("A fixed absolute audit root is required.", nameof(RootPath));
        }

        if (MaximumEntries is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumEntries));
        if (MaximumBytes is < 16 * 1024 or > 64L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumBytes));
    }
}
