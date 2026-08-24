namespace RmsSupportHub.Pos.Domain.Exceptions;

public sealed class RmsDatabaseAuditUnavailableException()
    : InvalidOperationException("Required RMS database audit recording is unavailable.");
