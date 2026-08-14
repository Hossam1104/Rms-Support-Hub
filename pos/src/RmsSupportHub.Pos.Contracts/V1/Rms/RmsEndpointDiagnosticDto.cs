using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsEndpointDiagnosticDto(
    bool Configured,
    string? Endpoint,
    EvidenceDto Reachability);
