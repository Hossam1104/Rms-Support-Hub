namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsConnectivityDto(
    RmsEndpointDiagnosticDto MainServer,
    RmsEndpointDiagnosticDto BranchServer);
