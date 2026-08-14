using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsDatabaseDiagnosticDto(
    string ExpectedDatabase,
    string? ConfiguredDatabase,
    string? ServerDisplay,
    bool Configured,
    bool? DatabaseNameMatches,
    RmsDatabaseDiagnosticStatus ConnectivityStatus,
    EvidenceDto Evidence);
