using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

[Collection("Agent service action runtime")]
public sealed class ServiceActionEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public ServiceActionEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdministratorCanControlAnAllowListedServiceWithAOneUseToken()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        manager.ControlBehavior = null;
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var token = await IssueServiceTokenAsync(client, serviceId);

        using var response = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("accepted", body.GetProperty("outcome").GetString());
        Assert.Equal(ServiceActionCodes.Accepted, body.GetProperty("code").GetString());
        Assert.Contains(
            manager.ControlCalls,
            call => call.ServiceName == "RMS.Downloader" && call.Action == ServiceControlAction.Start);
        Assert.DoesNotContain("RMS.Downloader", body.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MutationTokenIsConsumedBeforeTheTypedControlDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var token = await IssueServiceTokenAsync(client, serviceId);
        var store = _factory.Services.GetRequiredService<IMutationTokenStore>();
        MutationTokenConsumeResult? replayAtDispatch = null;
        manager.ControlBehavior = (_, _, _) =>
        {
            replayAtDispatch = store.TryConsume(new MutationTokenValidationRequest(
                token,
                FakeAuthenticationHandler.DefaultSid,
                AgentWebApplicationFactory.SupportHubOrigin,
                "POST",
                ServiceActionOperation.OperationId,
                $"/api/v1/services/{serviceId}/actions"));
            return Task.CompletedTask;
        };

        try
        {
            using var response = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(replayAtDispatch);
            Assert.Equal(MutationTokenFailure.Unknown, replayAtDispatch.Value.Failure);
            Assert.Single(manager.ControlCalls);
        }
        finally
        {
            manager.ControlBehavior = null;
        }
    }

    [Fact]
    public async Task StateInvalidActionReturnsNotAttemptedWithoutDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        manager.ControlBehavior = null;
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.BranchService");
        var token = await IssueServiceTokenAsync(client, serviceId);

        using var response = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("notAttempted", body.GetProperty("outcome").GetString());
        Assert.Equal(ServiceActionCodes.ActionNotAllowed, body.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task NonAdministratorAndUnauthenticatedCallersCannotReachTheMutationRuntime()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var nonAdmin = _factory.CreateNonAdminClient();
        using var nonAdminResponse = await SendActionAsync(
            nonAdmin,
            ServiceAllowList.ToServiceId("RMS.Downloader"),
            ServiceActionKind.Start,
            token: null);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdminResponse.StatusCode);

        using var unauthenticated = _factory.CreateSecureClient();
        unauthenticated.DefaultRequestHeaders.Add("Origin", AgentWebApplicationFactory.SupportHubOrigin);
        using var unauthenticatedResponse = await SendActionAsync(
            unauthenticated,
            ServiceAllowList.ToServiceId("RMS.Downloader"),
            ServiceActionKind.Start,
            token: null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task MissingTokenAndUnresolvableSidAreRejectedWithoutDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");

        using var missingToken = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token: null);
        var missingTokenBody = await ReadJsonAsync(missingToken);
        Assert.Equal(HttpStatusCode.Forbidden, missingToken.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, missingTokenBody.GetProperty("code").GetString());

        using var unresolvedSidClient = _factory.CreateClientWithSid("not-a-windows-sid");
        using var unresolvedSid = await SendActionAsync(
            unresolvedSidClient,
            serviceId,
            ServiceActionKind.Start,
            token: null);
        var unresolvedSidBody = await ReadJsonAsync(unresolvedSid);
        Assert.Equal(HttpStatusCode.Forbidden, unresolvedSid.StatusCode);
        Assert.Equal(AgentProblemCodes.WindowsSidUnavailable, unresolvedSidBody.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task PrincipalAndOriginMismatchesAreRejectedWithoutDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var issuer = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var token = await IssueServiceTokenAsync(issuer, serviceId);

        using var differentPrincipal = _factory.CreateClientWithSid(
            "S-1-5-21-1111111111-2222222222-3333333333-1002");
        using var principalMismatch = await SendActionAsync(
            differentPrincipal,
            serviceId,
            ServiceActionKind.Start,
            token);
        var principalBody = await ReadJsonAsync(principalMismatch);
        Assert.Equal(HttpStatusCode.Forbidden, principalMismatch.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, principalBody.GetProperty("code").GetString());

        var originToken = await IssueServiceTokenAsync(issuer, serviceId);
        using var wrongOrigin = _factory.CreateAdminClient();
        wrongOrigin.DefaultRequestHeaders.Remove("Origin");
        wrongOrigin.DefaultRequestHeaders.Add("Origin", "https://untrusted.integration.test");
        using var originMismatch = await SendActionAsync(wrongOrigin, serviceId, ServiceActionKind.Start, originToken);
        var originBody = await ReadJsonAsync(originMismatch);
        Assert.Equal(HttpStatusCode.Forbidden, originMismatch.StatusCode);
        Assert.Equal(AgentProblemCodes.OriginRejected, originBody.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task RawServiceNamesAndUnknownOpaqueTargetsAreNotAccepted()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();

        using var rawToken = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = ServiceActionOperation.OperationId, targetId = "RMS.Downloader" });
        var rawTokenBody = await ReadJsonAsync(rawToken);
        Assert.Equal(HttpStatusCode.BadRequest, rawToken.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTargetInvalid, rawTokenBody.GetProperty("code").GetString());

        using var unknownTarget = await SendActionAsync(
            client,
            "svc-0000000000000000",
            ServiceActionKind.Start,
            token: null);
        var unknownTargetBody = await ReadJsonAsync(unknownTarget);
        Assert.Equal(HttpStatusCode.OK, unknownTarget.StatusCode);
        Assert.Equal(ServiceActionCodes.TargetNotAllowListed, unknownTargetBody.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task TargetBoundTokenCannotBeUsedForAnotherServiceOrReplayed()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();
        var downloaderId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var branchId = ServiceAllowList.ToServiceId("RMS.BranchService");
        var token = await IssueServiceTokenAsync(client, downloaderId);

        using var wrongTarget = await SendActionAsync(client, branchId, ServiceActionKind.Stop, token);
        var wrongTargetBody = await ReadJsonAsync(wrongTarget);
        Assert.Equal(HttpStatusCode.Forbidden, wrongTarget.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, wrongTargetBody.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);

        using var replay = await SendActionAsync(client, downloaderId, ServiceActionKind.Start, token);
        var replayBody = await ReadJsonAsync(replay);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, replayBody.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task RepeatingAnIdempotencyKeyReturnsTheOriginalResponseWithoutAnotherDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        manager.ControlBehavior = null;
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var key = UniqueKey();
        var token = await IssueServiceTokenAsync(client, serviceId);

        using var first = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token, key);
        var firstBody = await ReadJsonAsync(first);
        using var repeated = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token: null, key);
        var repeatedBody = await ReadJsonAsync(repeated);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(firstBody.GetRawText(), repeatedBody.GetRawText());
        Assert.Single(manager.ControlCalls);
    }

    [Fact]
    public async Task ConflictingIdempotencyKeyIsNotDispatched()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        manager.ControlBehavior = null;
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var key = UniqueKey();

        var firstToken = await IssueServiceTokenAsync(client, serviceId);
        using var first = await SendActionAsync(client, serviceId, ServiceActionKind.Start, firstToken, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflictingToken = await IssueServiceTokenAsync(client, serviceId);
        using var conflict = await SendActionAsync(client, serviceId, ServiceActionKind.Restart, conflictingToken, key);
        var conflictBody = await ReadJsonAsync(conflict);

        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Equal(ServiceActionCodes.IdempotencyConflict, conflictBody.GetProperty("code").GetString());
        Assert.Single(manager.ControlCalls);
    }

    [Fact]
    public async Task PerServiceConcurrencyGateRejectsASecondDispatchWhileTheFirstIsInFlight()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.ControlBehavior = (_, _, _) =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        try
        {
            using var client = _factory.CreateAdminClient();
            var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
            var firstToken = await IssueServiceTokenAsync(client, serviceId);
            var firstTask = SendActionAsync(client, serviceId, ServiceActionKind.Start, firstToken, UniqueKey());
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var secondToken = await IssueServiceTokenAsync(client, serviceId);
            using var second = await SendActionAsync(client, serviceId, ServiceActionKind.Restart, secondToken, UniqueKey());
            var secondBody = await ReadJsonAsync(second);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(ServiceActionCodes.ServiceBusy, secondBody.GetProperty("code").GetString());
            Assert.Single(manager.ControlCalls);

            release.TrySetResult();
            using var first = await firstTask;
            var firstBody = await ReadJsonAsync(first);
            Assert.Equal("accepted", firstBody.GetProperty("outcome").GetString());
        }
        finally
        {
            release.TrySetResult();
            manager.ControlBehavior = null;
        }
    }

    [Fact]
    public async Task RejectedAndAmbiguousDispatchesUseTypedOutcomeTruthAndSafeDetails()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");

        manager.ControlBehavior = (_, _, _) =>
            Task.FromException(new ServiceControlRejectedException("service_control_rejected"));
        var rejectedToken = await IssueServiceTokenAsync(client, serviceId);
        using var rejected = await SendActionAsync(client, serviceId, ServiceActionKind.Start, rejectedToken, UniqueKey());
        var rejectedBody = await ReadJsonAsync(rejected);
        Assert.Equal("failed", rejectedBody.GetProperty("outcome").GetString());
        Assert.Equal(ServiceActionCodes.Failed, rejectedBody.GetProperty("code").GetString());

        manager.ControlBehavior = (_, _, _) =>
            Task.FromException(new InvalidOperationException("C:\\secret\\raw-service-command"));
        var unknownToken = await IssueServiceTokenAsync(client, serviceId);
        using var unknown = await SendActionAsync(client, serviceId, ServiceActionKind.Start, unknownToken, UniqueKey());
        var unknownBody = await ReadJsonAsync(unknown);
        var unknownText = unknownBody.GetRawText();
        Assert.Equal("outcomeUnknown", unknownBody.GetProperty("outcome").GetString());
        Assert.Equal(ServiceActionCodes.OutcomeUnknown, unknownBody.GetProperty("code").GetString());
        Assert.Contains("unknown", unknownBody.GetProperty("detail").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", unknownText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-service-command", unknownText, StringComparison.Ordinal);
        manager.ControlBehavior = null;
    }

    [Fact]
    public async Task InvalidIdempotencyKeyReturnsNotAttemptedWithoutConsumingDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        manager.ControlBehavior = null;
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var token = await IssueServiceTokenAsync(client, serviceId);

        using var response = await SendActionAsync(client, serviceId, ServiceActionKind.Start, token, "bad key");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ServiceActionCodes.InvalidIdempotencyKey, body.GetProperty("code").GetString());
        Assert.Empty(manager.ControlCalls);
    }

    [Fact]
    public async Task UnsupportedActionIsRejectedByTheTypedContractWithoutDispatch()
    {
        var manager = GetManager();
        manager.ControlCalls.Clear();
        using var client = _factory.CreateAdminClient();
        var serviceId = ServiceAllowList.ToServiceId("RMS.Downloader");
        var token = await IssueServiceTokenAsync(client, serviceId);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/services/{serviceId}/actions")
        {
            Content = JsonContent.Create(new { action = "delete", idempotencyKey = UniqueKey() })
        };
        request.Headers.Add(MutationTokenContract.HeaderName, token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(manager.ControlCalls);
    }

    private InMemoryServiceManager GetManager() =>
        (InMemoryServiceManager)_factory.Services.GetRequiredService<IServiceManager>();

    private static async Task<string> IssueServiceTokenAsync(HttpClient client, string serviceId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = ServiceActionOperation.OperationId, targetId = serviceId });
        var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body.GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendActionAsync(
        HttpClient client,
        string serviceId,
        ServiceActionKind action,
        string? token,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/services/{Uri.EscapeDataString(serviceId)}/actions")
        {
            Content = JsonContent.Create(new
            {
                action = action.ToString().ToLowerInvariant(),
                idempotencyKey = idempotencyKey ?? UniqueKey()
            })
        };
        if (token is not null)
        {
            request.Headers.Add(MutationTokenContract.HeaderName, token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    private static string UniqueKey() => $"int08-{Guid.NewGuid():N}";
}
