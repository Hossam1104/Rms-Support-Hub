using System.Net;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Http;

/// <summary>
/// Server-owned allowlist policy for the RMS backup trigger. It is deliberately an endpoint policy,
/// not a general-purpose proxy: a request must target the configured endpoint and redirects remain
/// on that endpoint's origin.
/// </summary>
public sealed class BackupApiEndpointPolicy
{
    private static readonly string[] BlockedHostNames =
    [
        "metadata.google.internal",
        "metadata.azure.internal",
        "instance-data",
        "instance-data.ec2.internal"
    ];

    public BackupApiEndpointPolicy(Uri approvedEndpoint)
    {
        ApprovedEndpoint = ValidateSyntax(approvedEndpoint, approvedEndpoint, isRedirect: false);
    }

    public Uri ApprovedEndpoint { get; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRedirects { get; init; } = 3;

    public static BackupApiEndpointPolicy FromConfiguredEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint))
        {
            throw new BackupApiPolicyException("downloader.endpoint_rejected");
        }

        return new BackupApiEndpointPolicy(endpoint);
    }

    public Uri ValidateInitial(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new BackupApiPolicyException("downloader.endpoint_rejected");
        }

        var validated = ValidateSyntax(uri, ApprovedEndpoint, isRedirect: false);
        if (!SameEndpoint(validated, ApprovedEndpoint))
        {
            throw new BackupApiPolicyException("downloader.endpoint_rejected");
        }

        return validated;
    }

    public Uri ValidateRedirect(Uri current, Uri location)
    {
        var target = location.IsAbsoluteUri ? location : new Uri(current, location);
        var validated = ValidateSyntax(target, ApprovedEndpoint, isRedirect: true);

        // Redirects are not a way to turn the Agent into a proxy. Keep them on the configured
        // origin and reject HTTPS downgrade even when the configured endpoint used HTTPS.
        if (!SameOrigin(validated, ApprovedEndpoint)
            || (string.Equals(current.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(validated.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BackupApiPolicyException("downloader.redirect_rejected");
        }

        return validated;
    }

    public async Task ValidateResolvedAddressesAsync(
        Uri target,
        IHostAddressResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var host = target.DnsSafeHost;
        if (IPAddress.TryParse(host, out var literal))
        {
            if (!IsPermittedAddress(literal, target, ApprovedEndpoint))
            {
                throw new BackupApiPolicyException("downloader.endpoint_rejected");
            }

            return;
        }

        var addresses = await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0 || addresses.Any(address => !IsPermittedAddress(address, target, ApprovedEndpoint)))
        {
            throw new BackupApiPolicyException("downloader.endpoint_rejected");
        }
    }

    /// <summary>
    /// Applies the same address policy at the instant a socket is opened. A private address is
    /// valid only when the logical connection host itself is an explicit IP literal; a hostname
    /// that resolves to private space is never accepted by the transport.
    /// </summary>
    public static bool IsPermittedConnectionAddress(string host, IPAddress address)
    {
        var normalized = NormalizeAddress(address);
        if (IsBlockedAddress(normalized))
        {
            return false;
        }

        return !IsPrivateAddress(normalized)
            || !address.IsIPv4MappedToIPv6 && TryParseHostLiteral(host, out _);
    }

    private static Uri ValidateSyntax(Uri uri, Uri approvedEndpoint, bool isRedirect)
    {
        if (!uri.IsAbsoluteUri
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Host.Any(char.IsWhiteSpace)
            || uri.Host.Any(char.IsControl)
            || uri.Host.Contains('\\', StringComparison.Ordinal)
            || uri.Host.Contains('%', StringComparison.Ordinal)
            || LooksLikeObfuscatedIp(uri.DnsSafeHost)
            || uri.DnsSafeHost.EndsWith(".", StringComparison.Ordinal))
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        if (uri.Port is < 1 or > 65535)
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        var approvedPort = approvedEndpoint.Port;
        if (uri.Port is not (80 or 443) && uri.Port != approvedPort)
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        if (BlockedHostNames.Any(blocked => string.Equals(blocked, uri.DnsSafeHost, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        if (Uri.CheckHostName(uri.DnsSafeHost) == UriHostNameType.Unknown)
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        if (IPAddress.TryParse(uri.DnsSafeHost, out var address) && !IsPermittedAddress(address, uri, approvedEndpoint))
        {
            throw new BackupApiPolicyException(isRedirect ? "downloader.redirect_rejected" : "downloader.endpoint_rejected");
        }

        return uri;
    }

    private static bool SameEndpoint(Uri left, Uri right) =>
        SameOrigin(left, right)
        && string.Equals(left.AbsolutePath, right.AbsolutePath, StringComparison.Ordinal)
        && string.Equals(left.Query, right.Query, StringComparison.Ordinal);

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.DnsSafeHost, right.DnsSafeHost, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    private static bool IsPermittedAddress(IPAddress address, Uri target, Uri approvedEndpoint)
    {
        var normalized = NormalizeAddress(address);
        if (IsBlockedAddress(normalized))
        {
            return false;
        }

        if (IsPrivateAddress(normalized))
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return false;
            }

            // A private RMS endpoint is allowed only because this exact target was explicitly
            // configured server-side as a literal address. A hostname that happens to resolve to
            // RFC1918 space is rejected so DNS cannot turn a public-looking endpoint into an SSRF
            // path; operators must explicitly configure the private literal target instead.
            return IPAddress.TryParse(approvedEndpoint.DnsSafeHost, out _)
                && SameOrigin(target, approvedEndpoint);
        }

        return true;
    }

    private static IPAddress NormalizeAddress(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsBlockedAddress(IPAddress normalized) =>
        IPAddress.IsLoopback(normalized)
        || normalized.Equals(IPAddress.Any)
        || normalized.Equals(IPAddress.IPv6Any)
        || normalized.IsIPv6LinkLocal
        || normalized.IsIPv6Multicast
        || IsMetadataAddress(normalized)
        || IsIpv4LinkLocal(normalized);

    private static bool TryParseHostLiteral(string host, out IPAddress? address) =>
        IPAddress.TryParse(host.Trim('[', ']'), out address);

    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
                || bytes[0] == 0;
        }

        return (bytes[0] & 0xFE) == 0xFC;
    }

    private static bool IsMetadataAddress(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && address.GetAddressBytes() is [169, 254, 169, 254];

    private static bool IsIpv4LinkLocal(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && address.GetAddressBytes() is [169, 254, _, _];

    private static bool LooksLikeObfuscatedIp(string host)
    {
        if (IPAddress.TryParse(host, out _)) return false;
        if (host.Contains(':', StringComparison.Ordinal)) return false;
        if (!host.Any(char.IsDigit)) return false;
        return host.All(character => char.IsDigit(character) || character is '.' or 'x' or 'X' or 'a' or 'b' or 'c' or 'd' or 'e' or 'f' or 'A' or 'B' or 'C' or 'D' or 'E' or 'F');
    }
}

public interface IHostAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

public sealed class SystemHostAddressResolver : IHostAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
}

public sealed class BackupApiPolicyException(string code)
    : DownloaderTriggerException(code, DownloaderTriggerState.NotAttempted)
{
}
