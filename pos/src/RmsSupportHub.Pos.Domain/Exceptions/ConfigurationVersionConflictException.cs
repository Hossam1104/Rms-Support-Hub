namespace RmsSupportHub.Pos.Domain.Exceptions;

/// <summary>
/// Thrown when a configuration or secret mutation's <c>ExpectedVersion</c> does not match the
/// current stored version (optimistic concurrency, plan section 5.5).
/// </summary>
public sealed class ConfigurationVersionConflictException(long expectedVersion, long actualVersion)
    : Exception($"Configuration version conflict: expected {expectedVersion}, actual {actualVersion}.")
{
    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}
