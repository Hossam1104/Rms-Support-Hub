using Microsoft.AspNetCore.Builder;
using RmsSupportHub.Api.Configuration;

namespace RmsSupportHub.Tests;

public sealed class TrustedForwardedHeadersConfigurationTests
{
    [Fact]
    public void EmptyAllowlistLeavesForwardedHeadersUntrusted()
    {
        var options = new ForwardedHeadersOptions();
        options.KnownProxies.Add(System.Net.IPAddress.Loopback);
        TrustedForwardedHeadersConfiguration.Apply(options, new TrustedForwardedHeadersOptions());

        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal(Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Fact]
    public void ExplicitProxyAndNetworkAllowlistIsAppliedWithoutForwardedFor()
    {
        var options = new ForwardedHeadersOptions();
        TrustedForwardedHeadersConfiguration.Apply(options, new TrustedForwardedHeadersOptions
        {
            KnownProxies = new() { "10.10.10.10" },
            KnownNetworks = new() { "10.10.20.0/24" }
        });

        Assert.Contains(System.Net.IPAddress.Parse("10.10.10.10"), options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.False(options.ForwardedHeaders.HasFlag(
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor));
    }
}
