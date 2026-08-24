namespace RmsSupportHub.Pos.Domain.Exceptions;

/// <summary>Signals that a completed backup exceeded the configured bounded artifact ceiling.</summary>
public sealed class RmsDatabaseBackupSizeLimitException()
    : InvalidOperationException("The RMS database backup exceeded the configured size limit.");
