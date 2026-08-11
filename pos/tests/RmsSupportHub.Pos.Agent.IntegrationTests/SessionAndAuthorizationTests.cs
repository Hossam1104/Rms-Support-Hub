using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Session;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class SessionAndAuthorizationTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public SessionAndAuthorizationTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedSessionRequest_IsRejected()
    {
        using var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("Origin", AgentWebApplicationFactory.SupportHubOrigin);

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdministrator_IsVisibleButNotAuthorized()
    {
        using var client = _factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/session");
        var body = await response.Content.ReadFromJsonAsync<SessionInfoDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body!.IsAuthorized);
        Assert.Equal("TESTDOMAIN\\standard-user", body.PrincipalName);
    }

    [Fact]
    public async Task AuthenticatedAdministrator_IsAuthorizedByTheServerSideGroupChecker()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/session");
        var body = await response.Content.ReadFromJsonAsync<SessionInfoDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body!.IsAuthorized);
        Assert.Equal("1.0", body.ApiVersion);
        Assert.Contains("1.0", body.SupportedApiVersions);
    }

    [Fact]
    public async Task InvalidSid_FailsClosedEvenWhenDisplayNameAndAdminHeaderArePresent()
    {
        using var client = _factory.CreateClientWithSid("not-a-windows-sid");

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void SidResolutionUsesCanonicalServerPrincipalClaim()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "display-name"), new Claim(ClaimTypes.PrimarySid, FakeAuthenticationHandler.DefaultSid)],
            "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(AgentPrincipal.TryGetSid(principal, out var sid));
        Assert.Equal(FakeAuthenticationHandler.DefaultSid, sid);
    }

    [Fact]
    public async Task LocalAdministratorsPolicyUsesTheInjectedGroupChecker()
    {
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity([new Claim("test-is-administrator", "true")], "test");

        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), PolicyNames.LocalAdministratorsOnly);

        Assert.True(result.Succeeded);
    }
}
