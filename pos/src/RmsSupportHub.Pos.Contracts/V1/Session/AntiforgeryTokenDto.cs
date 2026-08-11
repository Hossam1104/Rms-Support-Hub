namespace RmsSupportHub.Pos.Contracts.V1.Session;

/// <summary><c>GET /api/v1/antiforgery</c> response — mirrored into the configured request header on every mutation.</summary>
public sealed record AntiforgeryTokenDto(string Token);
