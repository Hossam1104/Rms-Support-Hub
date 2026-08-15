using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.MainServer;

/// <summary>
/// Server-owned Main Server profiles. These are not bound from browser input. Production remains
/// disabled until its separate certificate/credential and rollout gates are satisfied.
/// </summary>
public sealed class MainServerProfileOptions
{
    public string TestingProfileId { get; init; } = "main-server-testing";

    public string TestingBaseAddress { get; init; } = "https://main-server.testing.rms.local/rmsmainserverApi/";

    public string ProductionProfileId { get; init; } = "main-server-production";

    public string ProductionBaseAddress { get; init; } = "https://main-server.production.rms.local/rmsmainserverApi/";

    public string ClientName { get; init; } = "RMS+";

    public void Validate()
    {
        ValidateProfile(TestingProfileId, TestingBaseAddress);
        ValidateProfile(ProductionProfileId, ProductionBaseAddress);
        if (string.IsNullOrWhiteSpace(ClientName) || ClientName.Length > 64 || ClientName.Any(char.IsControl))
        {
            throw new ArgumentException("A safe Main Server client label is required.", nameof(ClientName));
        }
    }

    private static void ValidateProfile(string id, string address)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("A safe Main Server profile identifier is required.");
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.EndsWith("/rmsmainserverApi/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The server-owned Main Server profile must be an exact HTTPS API base.");
        }
    }
}
