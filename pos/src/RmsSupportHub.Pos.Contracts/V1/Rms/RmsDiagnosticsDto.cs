using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsDiagnosticsDto(
    RmsInstallationDto Installation,
    RmsConnectivityDto Connectivity,
    RmsDatabaseDiagnosticDto BranchDatabase,
    RmsDatabaseDiagnosticDto CashierDatabase,
    IReadOnlyList<ServiceSummaryDto> Services);
