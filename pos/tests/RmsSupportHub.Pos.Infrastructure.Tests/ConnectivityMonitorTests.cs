using RmsSupportHub.Pos.Infrastructure.Windows;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class ConnectivityMonitorTests
{
    [Theory]
    [InlineData("http://192.168.1.10", "192.168.1.10", 80)]
    [InlineData("https://server.example.com", "server.example.com", 443)]
    [InlineData("http://server.example.com:8080", "server.example.com", 8080)]
    [InlineData("192.168.0.5", "192.168.0.5", 80)]
    public void ParseHostPortResolvesDefaultPorts(string apiUrl, string expectedHost, int expectedPort)
    {
        var (host, port) = ConnectivityMonitor.ParseHostPort(apiUrl);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }
}
