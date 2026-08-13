using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RmsSupportHub.Pos.Agent;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class HostAndSecurityTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public HostAndSecurityTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CanonicalHostAndHttps_ReturnsLiveHealth()
    {
        using var client = _factory.CreateSecureClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("localhost:5001")]
    [InlineData("127.0.0.1:5001")]
    [InlineData("unknown.example.test:5001")]
    [InlineData("rms-pos-agent.localhost:5000")]
    public async Task NonCanonicalHost_IsRejected(string host)
    {
        using var client = _factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Host = host;

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HttpRequest_IsRejectedEvenWhenHostIsCanonical()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://rms-pos-agent.localhost:5001")
        });

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HostGateRejectsBeforeCallerCorrelationProcessing()
    {
        using var client = _factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Host = "localhost:5001";
        request.Headers.Add("X-Correlation-Id", "host-rejection-test");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("X-Correlation-Id", response.Headers.Select(header => header.Key));
        Assert.DoesNotContain("X-Frame-Options", response.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task PostCorrelationErrorCarriesCorrelationAndApiSecurityHeaders()
    {
        using var client = _factory.CreateAdminClient();
        client.DefaultRequestHeaders.Remove("Origin");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/session");
        request.Headers.Add("X-Correlation-Id", "origin-rejection-test");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("origin-rejection-test", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task ScalarDocumentAllowsOnlyLocalDocumentationAssets()
    {
        using var client = _factory.CreateSecureClient();

        var response = await client.GetAsync("/scalar/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'unsafe-inline'", policy);
        Assert.Contains("style-src 'self' 'unsafe-inline'", policy);
        Assert.Contains("connect-src 'self'", policy);
        Assert.Contains("font-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.DoesNotContain("googleapis", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gstatic", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.", policy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScalarDocumentWithoutTrailingSlashUsesTheSameLocalPolicy()
    {
        using var client = _factory.CreateSecureClient();

        var response = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("script-src 'self' 'unsafe-inline'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public void BindingPolicy_IsFixedToCanonicalHttpsHttp1LoopbackIdentity()
    {
        Assert.Equal("rms-pos-agent.localhost", AgentHostConstants.CanonicalHost);
        Assert.Equal(5001, AgentHostConstants.Port);
        Assert.Equal(AgentHostConstants.Port, LoopbackBinding.Port);
        Assert.Equal("https://rms-pos-agent.localhost:5001", AgentHostConstants.CanonicalOrigin);
    }
}
