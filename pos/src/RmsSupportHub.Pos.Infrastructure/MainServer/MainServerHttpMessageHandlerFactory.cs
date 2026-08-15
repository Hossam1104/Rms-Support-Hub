using System.Net;

namespace RmsSupportHub.Pos.Infrastructure.MainServer;

/// <summary>Bounded transport policy for the fixed server-owned Main Server profiles.</summary>
public static class MainServerHttpMessageHandlerFactory
{
    public static HttpMessageHandler Create() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 2
    };
}
