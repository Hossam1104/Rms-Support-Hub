using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Http;

/// <summary>Small seam for testing the address-approved socket connection without a real server.</summary>
public interface IConnectionBoundSocketConnector
{
    ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves and validates the actual target immediately before opening a socket. The returned
/// stream is handed to SocketsHttpHandler, which keeps the logical hostname for HTTP Host and TLS
/// SNI semantics while this connector controls the numeric address used by the socket.
/// </summary>
public sealed class ConnectionBoundSocketConnector : IConnectionBoundSocketConnector
{
    private readonly IHostAddressResolver _addressResolver;
    private readonly Func<IPAddress, int, CancellationToken, ValueTask<Stream>> _connect;

    public ConnectionBoundSocketConnector(
        IHostAddressResolver addressResolver,
        Func<IPAddress, int, CancellationToken, ValueTask<Stream>>? connect = null)
    {
        _addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        _connect = connect ?? ConnectTcpAsync;
    }

    public async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        IReadOnlyList<IPAddress> resolved;
        if (IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var literal))
        {
            resolved = [literal];
        }
        else
        {
            try
            {
                resolved = await _addressResolver.ResolveAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new BackupApiPolicyException(DownloaderFailureCodes.EndpointRejected);
            }
        }

        var normalized = resolved
            .Where(address => address is not null)
            .Select(address => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address)
            .ToList();
        if (normalized.Count == 0
            || normalized.Any(address => !BackupApiEndpointPolicy.IsPermittedConnectionAddress(endpoint.Host, address)))
        {
            throw new BackupApiPolicyException(DownloaderFailureCodes.EndpointRejected);
        }

        foreach (var address in normalized)
        {
            try
            {
                return await _connect(address, endpoint.Port, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BackupApiPolicyException)
            {
                throw;
            }
            catch (BackupApiRequestException)
            {
                throw;
            }
            catch (SocketException)
            {
                // A permitted DNS answer can have more than one address. Try the next permitted
                // answer without exposing the native failure details to the caller.
            }
            catch
            {
                throw new BackupApiRequestException(DownloaderFailureCodes.TriggerFailed);
            }
        }

        throw new BackupApiRequestException(DownloaderFailureCodes.TriggerFailed);
    }

    private static async ValueTask<Stream> ConnectTcpAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public static class BackupApiHttpMessageHandlerFactory
{
    public static SocketsHttpHandler Create(IHostAddressResolver addressResolver) =>
        Create(new ConnectionBoundSocketConnector(addressResolver));

    public static SocketsHttpHandler Create(IConnectionBoundSocketConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            // A proxy would move the socket target away from the request hostname that this
            // callback validates. The Agent trigger is a direct, server-configured connection.
            UseProxy = false,
            ConnectCallback = (context, cancellationToken) =>
                connector.ConnectAsync(context.DnsEndPoint, cancellationToken)
        };
        return handler;
    }
}
