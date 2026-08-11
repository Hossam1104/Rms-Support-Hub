namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Internal adapter credentials. This type must never cross an Agent/browser contract boundary.
/// </summary>
public sealed record RemoteConnectionInfo(
    string ServerIp,
    string Username,
    string Password,
    string? ApprovedRootFolder = null);
