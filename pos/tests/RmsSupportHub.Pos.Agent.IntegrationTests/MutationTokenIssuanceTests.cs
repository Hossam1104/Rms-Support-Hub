using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class MutationTokenIssuanceTests : IClassFixture<AgentWebApplicationFactory>
{
    private const string OperationId = "integration.test-mutation";
    private readonly AgentWebApplicationFactory _factory;

    public MutationTokenIssuanceTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedIssuance_IsRejected()
    {
        using var client = _factory.CreateSecureClient();

        var response = await IssueAsync(client, OperationId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdministratorIssuance_IsRejected()
    {
        using var client = _factory.CreateNonAdminClient();

        var response = await IssueAsync(client, OperationId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorWithoutCanonicalSid_IsRejectedWithStableCode()
    {
        using var client = _factory.CreateClientWithSid("not-a-windows-sid");

        var response = await IssueAsync(client, OperationId);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AgentProblemCodes.WindowsSidUnavailable, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MissingOrigin_IsRejectedBeforeTokenIssuance()
    {
        using var client = _factory.CreateAdminClient();
        client.DefaultRequestHeaders.Remove("Origin");

        var response = await IssueAsync(client, OperationId);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AgentProblemCodes.OriginRejected, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownOperationId_IsRejectedWithoutIssuingAToken()
    {
        using var client = _factory.CreateAdminClient();

        var response = await IssueAsync(client, "configuration.update");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AgentProblemCodes.OperationNotSupported, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisteredOperationIssuesOpaqueResponseWithServerOwnedTargetMethod()
    {
        using var client = _factory.CreateAdminClient();

        var methodBoundToken = await IssueAsync(client, OperationId);
        var methodBoundBody = await ReadJsonAsync(methodBoundToken);
        var methodBoundValue = methodBoundBody.GetProperty("token").GetString();
        Assert.Equal(HttpStatusCode.OK, methodBoundToken.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(methodBoundValue));

        var store = _factory.Services.GetRequiredService<IMutationTokenStore>();
        var wrongMethod = store.TryConsume(new MutationTokenValidationRequest(
            methodBoundValue!,
            TestSupport.FakeAuthenticationHandler.DefaultSid,
            AgentWebApplicationFactory.SupportHubOrigin,
            "POST",
            OperationId));
        Assert.Equal(MutationTokenFailure.Mismatch, wrongMethod.Failure);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/security/mutation-token")
        {
            Content = JsonContent.Create(new
            {
                operationId = OperationId,
                method = "POST",
                targetPath = "/api/v1/configuration"
            })
        };
        var response = await client.SendAsync(request);
        var json = await ReadJsonAsync(response);
        var properties = json.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["token", "expiresAtUtc"], properties);

        var issuedToken = json.GetProperty("token").GetString();
        var expiresAtUtc = json.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.False(string.IsNullOrWhiteSpace(issuedToken));
        Assert.True(expiresAtUtc > DateTimeOffset.UtcNow);

        var correctMethod = store.TryConsume(new MutationTokenValidationRequest(
            issuedToken!,
            TestSupport.FakeAuthenticationHandler.DefaultSid,
            AgentWebApplicationFactory.SupportHubOrigin,
            "PUT",
            OperationId));
        Assert.True(correctMethod.Succeeded);
    }

    private static Task<HttpResponseMessage> IssueAsync(HttpClient client, string operationId) =>
        client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
