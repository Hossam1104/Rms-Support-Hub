namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public enum RmsDatabaseDiagnosticStatus
{
    NotConfigured,
    ConfigurationInvalid,
    DatabaseNameMismatch,
    Reachable,
    AuthenticationFailed,
    DatabaseUnavailable,
    Unreachable
}
