using System.Net;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ProblemDetailsAndCorrelationTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public ProblemDetailsAndCorrelationTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ClientCorrelationIdIsReturnedWithoutSensitiveRequestData()
    {
        using var client = _factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "safe-correlation-id");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("safe-correlation-id", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.DoesNotContain("rms-pos-agent.localhost", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidHostReturnsSafeProblemDetails()
    {
        using var client = _factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Host = "127.0.0.1:5001";

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificate", body, StringComparison.OrdinalIgnoreCase);
    }
}
