using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace RmsSupportHub.Api.Configuration;

/// <summary>Applies the server-owned forwarded-scheme allowlist to the
/// standard ASP.NET Core middleware. X-Forwarded-For is intentionally not
/// enabled: source throttling must use the server-observed connection address,
/// not a browser-controlled header.</summary>
public static class TrustedForwardedHeadersConfiguration
{
    public static void Apply(ForwardedHeadersOptions options, TrustedForwardedHeadersOptions configured)
    {
        var hasTrustedSource = (configured.KnownProxies?.Count ?? 0) > 0
            || (configured.KnownNetworks?.Count ?? 0) > 0;
        options.ForwardedHeaders = hasTrustedSource
            ? ForwardedHeaders.XForwardedProto
            : ForwardedHeaders.None;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
#pragma warning disable ASPDEPR005
        options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
        options.KnownIPNetworks.Clear();

        foreach (var proxy in configured.KnownProxies ?? new List<string>())
            options.KnownProxies.Add(IPAddress.Parse(proxy));

        foreach (var network in configured.KnownNetworks ?? new List<string>())
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network.AsSpan()));
    }

    internal static bool IsValidProxy(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && IPAddress.TryParse(value, out _);

    internal static bool IsValidNetwork(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || !System.Net.IPNetwork.TryParse(value.AsSpan(), out var network))
        {
            return false;
        }

        // A /0 network is an implicit wildcard and would defeat the purpose
        // of requiring an explicit bounded proxy/network allowlist.
        return network.PrefixLength > 0;
    }
}
