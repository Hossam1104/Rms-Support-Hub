using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class RuntimeResponseShapeTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public RuntimeResponseShapeTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedSessionUsesTheNegotiateChallengeShape()
    {
        using var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("Origin", AgentWebApplicationFactory.SupportHubOrigin);

        var response = await client.GetAsync("/api/v1/session");

        await AssertBodylessFrameworkResponseAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnauthenticatedMutationIssuanceUsesTheNegotiateChallengeShape()
    {
        using var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("Origin", AgentWebApplicationFactory.SupportHubOrigin);

        var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = "int06i.post-remediation.unknown" });

        await AssertBodylessFrameworkResponseAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedNonAdministratorMutationUsesTheBodylessPolicyForbidShape()
    {
        using var client = _factory.CreateNonAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = "int06i.post-remediation.unknown" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task SidUnavailableSessionUsesTheAgentProblemDetailsContract()
    {
        using var client = _factory.CreateClientWithSid("not-a-windows-sid", isAdministrator: true);

        var response = await client.GetAsync("/api/v1/session");
        var body = await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Forbidden,
            AgentProblemCodes.WindowsSidUnavailable);

        Assert.Equal("The authenticated Windows SID could not be resolved.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnknownOperationUsesTheAgentProblemDetailsContractWithoutIssuingAToken()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = "int06i.post-remediation.unknown" });
        var body = await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            AgentProblemCodes.OperationNotSupported);

        Assert.False(body.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task MutationTokenCapacityUsesTheDocumentedProblemDetailsContract()
    {
        using var factory = new AgentWebApplicationFactory();
        using var client = factory.CreateAdminClient();
        var store = factory.Services.GetRequiredService<IMutationTokenStore>();
        var options = new MutationTokenOptions();

        for (var index = 0; index < options.MaxTokens; index++)
        {
            store.Issue(
                FakeAuthenticationHandler.DefaultSid,
                AgentWebApplicationFactory.SupportHubOrigin,
                "PUT",
                "capacity-test");
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = "integration.test-mutation" });
        var body = await AssertProblemDetailsAsync(
            response,
            (HttpStatusCode)429,
            AgentProblemCodes.MutationTokenCapacity);

        Assert.False(body.TryGetProperty("token", out _));
    }

    private static async Task AssertBodylessFrameworkResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            challenge => string.Equals(challenge.Scheme, "Negotiate", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<JsonElement> AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var body = document.RootElement.Clone();
        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());
        return body;
    }
}
