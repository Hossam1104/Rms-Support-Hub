using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class DownloaderMaintenanceEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory factory;

    public DownloaderMaintenanceEndpointTests(AgentWebApplicationFactory factory)
    {
        this.factory = factory;
        factory.EnableDownloaderCredential();
    }

    [Fact]
    public async Task AdministratorCanDownloadAnOpaqueArtifactWithoutReceivingRemoteTransportDetails()
    {
        using var client = factory.CreateAdminClient();

        using var branchesResponse = await client.GetAsync("/api/v1/downloads/branches");
        var branches = await ReadJsonAsync(branchesResponse);
        Assert.Equal(HttpStatusCode.OK, branchesResponse.StatusCode);
        Assert.Contains(branches.EnumerateArray(), branch => branch.GetProperty("branchCode").GetString() == "BR-INT");
        Assert.DoesNotContain("\\\\rdb\\backups", branches.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var token = await IssueTokenAsync(client, "downloader.batch.trigger");
        var idempotencyKey = UniqueKey();
        using var start = CreateMutationRequest(
            HttpMethod.Post,
            "/api/v1/downloads/batches",
            token,
            new { branchCodes = new[] { "BR-INT" }, idempotencyKey });
        using var startResponse = await client.SendAsync(start);
        var accepted = await ReadJsonAsync(startResponse);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var operationId = accepted.GetProperty("operationId").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(operationId), accepted.GetRawText());
        var completed = await WaitForFinalAsync(client, $"/api/v1/downloads/operations/{operationId}");
        Assert.Equal("completed", completed.GetProperty("outcome").GetString());
        var artifactId = completed
            .GetProperty("downloaderOutcome")
            .GetProperty("branches")[0]
            .GetProperty("artifactId")
            .GetString();
        Assert.Matches("^[0-9a-f]{32}$", artifactId);

        var operationText = completed.GetRawText();
        Assert.DoesNotContain("rdb", operationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backups", operationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.0.2.10", operationText, StringComparison.Ordinal);
        Assert.DoesNotContain("password", operationText, StringComparison.OrdinalIgnoreCase);

        using var artifactResponse = await client.GetAsync($"/api/v1/artifacts/{artifactId}");
        var artifactBytes = await artifactResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, artifactResponse.StatusCode);
        Assert.Equal("application/zip", artifactResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, artifactBytes[..4]);

        using var missingArtifact = await client.GetAsync("/api/v1/artifacts/00000000000000000000000000000000");
        Assert.Equal(HttpStatusCode.NotFound, missingArtifact.StatusCode);

        using var replayedToken = CreateMutationRequest(
            HttpMethod.Post,
            "/api/v1/downloads/batches",
            token,
            new { branchCodes = new[] { "BR-INT" }, idempotencyKey = UniqueKey() });
        using var replayedTokenResponse = await client.SendAsync(replayedToken);
        var replayedTokenBody = await ReadJsonAsync(replayedTokenResponse);
        Assert.Equal(HttpStatusCode.Forbidden, replayedTokenResponse.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, replayedTokenBody.GetProperty("code").GetString());

        using var replay = await client.PostAsJsonAsync(
            "/api/v1/downloads/batches",
            new { branchCodes = new[] { "BR-INT" }, idempotencyKey });
        var replayBody = await ReadJsonAsync(replay);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(operationId, replayBody.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task DownloaderRejectsUnauthorizedCallersInvalidBranchesAndInvalidMutationTokens()
    {
        using var anonymous = factory.CreateSecureClient();
        using var anonymousResponse = await anonymous.GetAsync("/api/v1/downloads/branches");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var nonAdmin = factory.CreateNonAdminClient();
        using var nonAdminResponse = await nonAdmin.GetAsync("/api/v1/downloads/branches");
        Assert.Equal(HttpStatusCode.Forbidden, nonAdminResponse.StatusCode);

        using var client = factory.CreateAdminClient();
        var token = await IssueTokenAsync(client, "downloader.batch.trigger");
        using var invalidBranch = CreateMutationRequest(
            HttpMethod.Post,
            "/api/v1/downloads/batches",
            token,
            new { branchCodes = new[] { "NOT-APPROVED" }, idempotencyKey = UniqueKey() });
        using var invalidBranchResponse = await client.SendAsync(invalidBranch);
        var invalidBranchBody = await ReadJsonAsync(invalidBranchResponse);
        Assert.Equal(HttpStatusCode.OK, invalidBranchResponse.StatusCode);
        Assert.Equal("notAttempted", invalidBranchBody.GetProperty("outcome").GetString());
        Assert.Equal("downloader.branch_invalid", invalidBranchBody.GetProperty("errorCode").GetString());

        using var missingTokenResponse = await client.PostAsJsonAsync(
            "/api/v1/downloads/batches",
            new { branchCodes = new[] { "BR-INT" }, idempotencyKey = UniqueKey() });
        var missingTokenBody = await ReadJsonAsync(missingTokenResponse);
        Assert.Equal(HttpStatusCode.Forbidden, missingTokenResponse.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, missingTokenBody.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MaintenancePreviewChallengeAndTypedExecutionStayWithinFakeServerOwnedSeams()
    {
        using var client = factory.CreateAdminClient();

        using var previewResponse = await client.PostAsync("/api/v1/maintenance/cleanup/preview", content: null);
        var preview = await ReadJsonAsync(previewResponse);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview.GetProperty("ready").GetBoolean());
        Assert.Matches("^cleanup-[0-9]{3}$", preview.GetProperty("pathsToDelete")[0].GetString());
        Assert.DoesNotContain("maintenance", preview.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RMS.BranchService", preview.GetRawText(), StringComparison.Ordinal);

        var token = await IssueTokenAsync(client, "maintenance.cleanup.execute");
        var idempotencyKey = UniqueKey();
        using var execute = CreateMutationRequest(
            HttpMethod.Post,
            "/api/v1/maintenance/cleanup/execute",
            token,
            new
            {
                challengeId = preview.GetProperty("challengeId").GetString(),
                typedConfirmation = preview.GetProperty("confirmationPhrase").GetString(),
                idempotencyKey
            });
        using var executeResponse = await client.SendAsync(execute);
        var accepted = await ReadJsonAsync(executeResponse);
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);

        var operationId = accepted.GetProperty("operationId").GetString()!;
        var completed = await WaitForFinalAsync(client, $"/api/v1/maintenance/operations/{operationId}");
        Assert.Equal("completed", completed.GetProperty("outcome").GetString());
        Assert.True(completed.GetProperty("maintenanceOutcome").GetProperty("destructiveAttempted").GetBoolean());
        Assert.DoesNotContain("RMS.BranchService", completed.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", completed.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var fileSystem = factory.Services.GetRequiredService<InMemoryMaintenanceFileSystem>();
        Assert.NotEmpty(fileSystem.DeleteCalls);
    }

    [Fact]
    public async Task BranchResetUsesPreviewChallengeAndFakeDatabaseOnly()
    {
        using var client = factory.CreateAdminClient();
        using var previewResponse = await client.PostAsync("/api/v1/maintenance/reset/preview", content: null);
        var preview = await ReadJsonAsync(previewResponse);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview.GetProperty("ready").GetBoolean());
        Assert.Equal("BR-INT", preview.GetProperty("branchCode").GetString());
        Assert.DoesNotContain("RMS.BranchService", preview.GetRawText(), StringComparison.Ordinal);

        var token = await IssueTokenAsync(client, "maintenance.branch-reset.execute");
        using var execute = CreateMutationRequest(
            HttpMethod.Post,
            "/api/v1/maintenance/reset/execute",
            token,
            new
            {
                challengeId = preview.GetProperty("challengeId").GetString(),
                typedConfirmation = preview.GetProperty("confirmationPhrase").GetString(),
                idempotencyKey = UniqueKey()
            });
        using var response = await client.SendAsync(execute);
        var accepted = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operation = await WaitForFinalAsync(
            client,
            $"/api/v1/maintenance/operations/{accepted.GetProperty("operationId").GetString()}");
        Assert.Equal("completed", operation.GetProperty("outcome").GetString());

        var database = factory.Services.GetRequiredService<InMemoryMaintenanceDatabase>();
        Assert.Single(database.ResetCalls);
        Assert.Equal("RmsBranchSrv", database.ResetCalls.Single().DatabaseName);
    }

    [Fact]
    public async Task DownloaderPreservesAmbiguousTriggerTruthAndRejectsConcurrentDispatch()
    {
        var api = factory.Services.GetRequiredService<InMemoryBackupApiClient>();
        api.Result = new(DownloaderTriggerState.OutcomeUnknown, "synthetic.unknown");
        try
        {
            using var unknownClient = factory.CreateAdminClient();
            var unknownToken = await IssueTokenAsync(unknownClient, "downloader.batch.trigger");
            using var unknownRequest = CreateMutationRequest(
                HttpMethod.Post,
                "/api/v1/downloads/batches",
                unknownToken,
                new { branchCodes = new[] { "BR-INT" }, idempotencyKey = UniqueKey() });
            using var unknownResponse = await unknownClient.SendAsync(unknownRequest);
            var accepted = await ReadJsonAsync(unknownResponse);
            var unknown = await WaitForFinalAsync(
                unknownClient,
                $"/api/v1/downloads/operations/{accepted.GetProperty("operationId").GetString()}");
            Assert.Equal("outcomeUnknown", unknown.GetProperty("outcome").GetString());
            Assert.Equal("outcomeUnknown", unknown.GetProperty("downloaderOutcome").GetProperty("triggerState").GetString());
        }
        finally
        {
            api.Result = new(DownloaderTriggerState.Accepted);
        }

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DownloaderTriggerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.TriggerBehavior = (_, _, _) =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        try
        {
            using var client = factory.CreateAdminClient();
            var firstToken = await IssueTokenAsync(client, "downloader.batch.trigger");
            using var firstRequest = CreateMutationRequest(
                HttpMethod.Post,
                "/api/v1/downloads/batches",
                firstToken,
                new { branchCodes = new[] { "BR-INT" }, idempotencyKey = UniqueKey() });
            var firstResponseTask = client.SendAsync(firstRequest);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var secondToken = await IssueTokenAsync(client, "downloader.batch.trigger");
            using var secondRequest = CreateMutationRequest(
                HttpMethod.Post,
                "/api/v1/downloads/batches",
                secondToken,
                new { branchCodes = new[] { "BR-INT" }, idempotencyKey = UniqueKey() });
            using var secondResponse = await client.SendAsync(secondRequest);
            var secondBody = await ReadJsonAsync(secondResponse);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.Equal("downloader.operation_busy", secondBody.GetProperty("errorCode").GetString());

            release.TrySetResult(new(DownloaderTriggerState.Accepted));
            using var firstResponse = await firstResponseTask;
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            var firstBody = await ReadJsonAsync(firstResponse);
            var firstOperationId = firstBody.GetProperty("operationId").GetString()!;
            var firstCompleted = await WaitForFinalAsync(client, $"/api/v1/downloads/operations/{firstOperationId}");
            Assert.Equal("completed", firstCompleted.GetProperty("outcome").GetString());
        }
        finally
        {
            release.TrySetResult(new(DownloaderTriggerState.Accepted));
            api.TriggerBehavior = null;
        }
    }

    private static HttpRequestMessage CreateMutationRequest(
        HttpMethod method,
        string path,
        string token,
        object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(MutationTokenContract.HeaderName, token);
        return request;
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, string operationId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId });
        var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body.GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> WaitForFinalAsync(HttpClient client, string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Expected operation JSON at {path}, got {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
            }

            var body = await ReadJsonAsync(response);
            var state = body.GetProperty("state").GetString();
            if (state is "completed" or "failed" or "outcomeUnknown" or "notAttempted") return body;
            await Task.Delay(10);
        }

        throw new TimeoutException("The synthetic Agent operation did not reach a terminal state.");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Expected JSON from {(int)response.StatusCode} {response.ReasonPhrase}; response body was empty.");
        }

        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static string UniqueKey() => $"integration-{Guid.NewGuid():N}";
}
