namespace RmsSupportHub.Pos.Domain.Exceptions;

/// <summary>
/// Stable, sanitized failure boundary for the server-owned backup repository. Infrastructure
/// adapters translate SMB and bounded I/O failures into this application-facing abstraction.
/// </summary>
public sealed class BackupRepositoryException(string code) : InvalidOperationException("The backup repository could not be accessed.")
{
    public string Code { get; } = code;
}
