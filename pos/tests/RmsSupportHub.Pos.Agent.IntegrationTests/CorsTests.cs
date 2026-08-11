using System.Net;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class CorsTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public CorsTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApprovedPreflight_IsAnonymousAndExactOriginOnly()
    {
        using var client = _factory.CreateSecureClient();
        using var request = CreatePreflight("GET", "X-Correlation-Id");

        var response = await client.SendAsync(request);


        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK);
        Assert.Equal(AgentWebApplicationFactory.SupportHubOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("Origin", response.Headers.GetValues("Vary").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GET", response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownOriginPreflight_IsRejectedWithoutReflection()
    {
        using var client = _factory.CreateSecureClient();
        using var request = CreatePreflight("GET", "X-Correlation-Id", "https://unknown.example.test");

        var response = await client.SendAsync(request);

        Assert.False((int)response.StatusCode is >= 200 and < 300);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task WildcardOriginPreflight_IsRejected()
    {
        using var client = _factory.CreateSecureClient();
        using var request = CreatePreflight("GET", "X-Correlation-Id", "*");

        var response = await client.SendAsync(request);

        Assert.False((int)response.StatusCode is >= 200 and < 300);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnsupportedMethodPreflight_IsRejected()
    {
        using var client = _factory.CreateSecureClient();
        using var request = CreatePreflight("DELETE", "X-Correlation-Id");

        var response = await client.SendAsync(request);

        Assert.False((int)response.StatusCode is >= 200 and < 300);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnsupportedHeaderPreflight_IsRejected()
    {
        using var client = _factory.CreateSecureClient();
        using var request = CreatePreflight("GET", "X-Not-Allowed");

        var response = await client.SendAsync(request);

        Assert.False((int)response.StatusCode is >= 200 and < 300);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task BrowserApiRequestWithoutOrigin_IsRejectedIndependentlyOfCors()
    {
        using var client = _factory.CreateAdminClient();
        client.DefaultRequestHeaders.Remove("Origin");

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApprovedOriginApplicationRequestGetsCredentialedCorsHeaders()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgentWebApplicationFactory.SupportHubOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    private static HttpRequestMessage CreatePreflight(
        string method,
        string requestedHeaders,
        string origin = AgentWebApplicationFactory.SupportHubOrigin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/session");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", requestedHeaders);
        return request;
    }
}
