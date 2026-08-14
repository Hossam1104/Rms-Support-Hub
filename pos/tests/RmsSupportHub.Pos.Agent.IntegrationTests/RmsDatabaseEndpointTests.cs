using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using DomainRmsDatabaseDiagnosticStatus = RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

[Collection("Agent database runtime")]
public sealed class RmsDatabaseEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory factory;

    public RmsDatabaseEndpointTests(AgentWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task DatabaseWorkspaceRequiresAdministratorAndExactOrigin()
    {
        ResetFakes();

        using var anonymous = factory.CreateSecureClient();
        using var unauthenticated = await anonymous.GetAsync("/api/v1/rms/databases/branch");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var nonAdmin = factory.CreateNonAdminClient();
        using var forbidden = await nonAdmin.GetAsync("/api/v1/rms/databases/branch");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var wrongOrigin = factory.CreateAdminClient();
        wrongOrigin.DefaultRequestHeaders.Remove("Origin");
        wrongOrigin.DefaultRequestHeaders.Add("Origin", "https://untrusted.integration.test");
        using var originRejected = await wrongOrigin.GetAsync("/api/v1/rms/databases/branch");
        var problem = await ReadJsonAsync(originRejected);
        Assert.Equal(HttpStatusCode.Forbidden, originRejected.StatusCode);
        Assert.Equal(AgentProblemCodes.OriginRejected, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task BranchAndCashierBackupsUseTypedTargetsAndReturnOpaqueArtifacts()
    {
        ResetFakes();
        using var client = factory.CreateAdminClient();

        var branch = await RunBackupAsync(client, "branch");
        var cashier = await RunBackupAsync(client, "cashier");

        Assert.Equal("branch", branch.GetProperty("target").GetString());
        Assert.Equal("completed", branch.GetProperty("outcome").GetString());
        Assert.Equal("RmsBranchSrv", branch.GetProperty("artifact").GetProperty("displayName").GetString()!.Split('_')[0]);
        Assert.Equal("cashier", cashier.GetProperty("target").GetString());
        Assert.Equal("completed", cashier.GetProperty("outcome").GetString());
        Assert.Equal("RmsCashierSrv", cashier.GetProperty("artifact").GetProperty("displayName").GetString()!.Split('_')[0]);

        var combined = branch.GetRawText() + cashier.GetRawText();
        Assert.DoesNotContain("ServerPath", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", combined, StringComparison.OrdinalIgnoreCase);

        using var workspaceResponse = await client.GetAsync("/api/v1/rms/databases/branch");
        var workspace = await ReadJsonAsync(workspaceResponse);
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
        Assert.Contains(
            workspace.GetProperty("approvedBackups").EnumerateArray(),
            artifact => artifact.GetProperty("artifactId").GetString() == branch.GetProperty("artifact").GetProperty("artifactId").GetString());
    }

    [Fact]
    public async Task BackupRefusesMismatchAndUnavailableDatabaseBeforeTokenConsumptionOrDispatch()
    {
        ResetFakes();
        var diagnostics = GetDiagnostics();
        var sql = GetSql();
        using var client = factory.CreateAdminClient();

        diagnostics.Status = DomainRmsDatabaseDiagnosticStatus.DatabaseNameMismatch;
        diagnostics.DatabaseNameMatches = false;
        var mismatchToken = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, "branch");
        var mismatch = await SendBackupAsync(client, "branch", mismatchToken, UniqueKey());
        Assert.Equal("database_name_mismatch", mismatch.GetProperty("errorCode").GetString());
        Assert.Equal("notAttempted", mismatch.GetProperty("outcome").GetString());
        Assert.Empty(sql.BackupCalls);

        diagnostics.Status = DomainRmsDatabaseDiagnosticStatus.DatabaseUnavailable;
        diagnostics.DatabaseNameMatches = null;
        var unavailableToken = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, "branch");
        var unavailable = await SendBackupAsync(client, "branch", unavailableToken, UniqueKey());
        Assert.Equal("database_unavailable", unavailable.GetProperty("errorCode").GetString());
        Assert.Empty(sql.BackupCalls);
    }

    [Fact]
    public async Task BackupReportsFailedAndOutcomeUnknownWithoutAutomaticRetry()
    {
        ResetFakes();
        var sql = GetSql();
        using var client = factory.CreateAdminClient();

        sql.BackupOutcome = RmsDatabaseSqlOutcome.Failed;
        var failed = await RunBackupAsync(client, "branch");
        Assert.Equal("failed", failed.GetProperty("outcome").GetString());
        Assert.Single(sql.BackupCalls);

        sql.BackupOutcome = RmsDatabaseSqlOutcome.OutcomeUnknown;
        var unknown = await RunBackupAsync(client, "cashier");
        Assert.Equal("outcomeUnknown", unknown.GetProperty("outcome").GetString());
        Assert.True(unknown.GetProperty("recoveryRequired").GetBoolean());
        Assert.Equal(2, sql.BackupCalls.Count);
    }

    [Fact]
    public async Task MutationTokenIsOneUseAndIdempotencyReturnsOriginalOperation()
    {
        ResetFakes();
        using var client = factory.CreateAdminClient();
        var key = UniqueKey();
        var token = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, "branch");

        using var request = CreateBackupRequest("branch", token, key);
        using var firstResponse = await client.SendAsync(request);
        var first = await ReadJsonAsync(firstResponse);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var replayRequest = CreateBackupRequest("branch", token, UniqueKey());
        using var replayResponse = await client.SendAsync(replayRequest);
        var replay = await ReadJsonAsync(replayResponse);
        Assert.Equal(HttpStatusCode.Forbidden, replayResponse.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTokenInvalid, replay.GetProperty("code").GetString());

        await WaitForFinalAsync(client, "branch", first.GetProperty("operationId").GetString()!);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/rms/databases/branch/backup",
            new RmsDatabaseBackupRequestDto(key));
        var duplicate = await ReadJsonAsync(duplicateResponse);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Equal(first.GetProperty("operationId").GetString(), duplicate.GetProperty("operationId").GetString());
        Assert.Single(GetSql().BackupCalls);
    }

    [Fact]
    public async Task RestoreRequiresApprovedArtifactExactConfirmationAndCoordinatesOnlyItsService()
    {
        ResetFakes();
        using var client = factory.CreateAdminClient();
        var backup = await RunBackupAsync(client, "branch");
        var artifactId = backup.GetProperty("artifact").GetProperty("artifactId").GetString()!;

        var badConfirmationToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var badConfirmation = await SendRestoreAsync(
            client,
            "branch",
            badConfirmationToken,
            artifactId,
            "restore branch",
            UniqueKey());
        Assert.Equal("restore_confirmation_invalid", badConfirmation.GetProperty("errorCode").GetString());
        Assert.Empty(GetSql().RestoreCalls);

        var token = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var restored = await SendRestoreAsync(
            client,
            "branch",
            token,
            artifactId,
            "RESTORE BRANCH DATABASE",
            UniqueKey());
        Assert.Equal("completed", restored.GetProperty("outcome").GetString());
        Assert.True(restored.GetProperty("destructiveAttempted").GetBoolean());

        var calls = GetServiceManager().ControlCalls.ToArray();
        Assert.Contains(calls, call => call.ServiceName == "RMS.BranchService" && call.Action == ServiceControlAction.Stop);
        Assert.Contains(calls, call => call.ServiceName == "RMS.BranchService" && call.Action == ServiceControlAction.Start);
        Assert.DoesNotContain(calls, call => call.ServiceName == "RMS.CashierService");
    }

    [Fact]
    public async Task RestoreProceedsWhenTargetDatabaseIsUnavailableOrMissingButServerIsReachable()
    {
        ResetFakes();
        var diagnostics = GetDiagnostics();
        using var client = factory.CreateAdminClient();
        var backup = await RunBackupAsync(client, "branch");
        var artifactId = backup.GetProperty("artifact").GetProperty("artifactId").GetString()!;

        // The target database diagnostic reports the database itself cannot be opened -- exactly
        // the condition a restore must be able to recover from -- while the server-only diagnostic
        // used to preflight Restore still reports the SQL Server master connection as reachable.
        diagnostics.Status = DomainRmsDatabaseDiagnosticStatus.DatabaseUnavailable;
        diagnostics.DatabaseNameMatches = null;
        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.Reachable;
        diagnostics.ServerDatabaseNameMatches = true;

        var token = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var restored = await SendRestoreAsync(
            client,
            "branch",
            token,
            artifactId,
            "RESTORE BRANCH DATABASE",
            UniqueKey());
        Assert.Equal("completed", restored.GetProperty("outcome").GetString());
        Assert.Single(GetSql().RestoreCalls);
    }

    [Fact]
    public async Task RestoreRejectsWhenServerUnreachableAuthenticationFailsOrNameMismatchesWithoutSqlDispatch()
    {
        ResetFakes();
        var diagnostics = GetDiagnostics();
        using var client = factory.CreateAdminClient();
        var backup = await RunBackupAsync(client, "branch");
        var artifactId = backup.GetProperty("artifact").GetProperty("artifactId").GetString()!;

        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.Unreachable;
        var unreachableToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var unreachable = await SendRestoreAsync(client, "branch", unreachableToken, artifactId, "RESTORE BRANCH DATABASE", UniqueKey());
        Assert.Equal("restore_server_unreachable", unreachable.GetProperty("errorCode").GetString());

        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.AuthenticationFailed;
        var authToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var auth = await SendRestoreAsync(client, "branch", authToken, artifactId, "RESTORE BRANCH DATABASE", UniqueKey());
        Assert.Equal("restore_server_authentication_failed", auth.GetProperty("errorCode").GetString());

        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.DatabaseNameMismatch;
        diagnostics.ServerDatabaseNameMatches = false;
        var mismatchToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var mismatch = await SendRestoreAsync(client, "branch", mismatchToken, artifactId, "RESTORE BRANCH DATABASE", UniqueKey());
        Assert.Equal("database_name_mismatch", mismatch.GetProperty("errorCode").GetString());

        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.ConfigurationInvalid;
        diagnostics.ServerDatabaseNameMatches = true;
        var configToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var config = await SendRestoreAsync(client, "branch", configToken, artifactId, "RESTORE BRANCH DATABASE", UniqueKey());
        Assert.Equal("restore_server_configuration_invalid", config.GetProperty("errorCode").GetString());

        Assert.Empty(GetSql().RestoreCalls);
    }

    [Fact]
    public async Task RestoreRejectsUnknownWrongTargetAndTraversalArtifactWithoutSqlDispatch()
    {
        ResetFakes();
        using var client = factory.CreateAdminClient();
        var backup = await RunBackupAsync(client, "branch");
        var branchArtifact = backup.GetProperty("artifact").GetProperty("artifactId").GetString()!;

        var wrongTargetToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "cashier");
        var wrongTarget = await SendRestoreAsync(
            client,
            "cashier",
            wrongTargetToken,
            branchArtifact,
            "RESTORE CASHIER DATABASE",
            UniqueKey());
        Assert.Equal("restore_backup_not_approved", wrongTarget.GetProperty("errorCode").GetString());

        var traversalToken = await IssueTokenAsync(client, RmsDatabaseOperation.RestoreOperationId, "branch");
        var traversal = await SendRestoreAsync(
            client,
            "branch",
            traversalToken,
            "..\\..\\outside.bak",
            "RESTORE BRANCH DATABASE",
            UniqueKey());
        Assert.Equal("restore_backup_not_approved", traversal.GetProperty("errorCode").GetString());
        Assert.Empty(GetSql().RestoreCalls);
    }

    [Fact]
    public async Task PerDatabaseConcurrencyRejectsSecondOperationWithoutSecondSqlDispatch()
    {
        ResetFakes();
        var sql = GetSql();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sql.BackupBehavior = (_, _) =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        try
        {
            using var client = factory.CreateAdminClient();
            var firstToken = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, "branch");
            var firstTask = client.SendAsync(CreateBackupRequest("branch", firstToken, UniqueKey()));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var secondToken = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, "branch");
            using var secondResponse = await client.SendAsync(CreateBackupRequest("branch", secondToken, UniqueKey()));
            var second = await ReadJsonAsync(secondResponse);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.Equal("database_operation_busy", second.GetProperty("errorCode").GetString());
            Assert.Single(sql.BackupCalls);

            release.TrySetResult();
            using var firstResponse = await firstTask;
            var first = await ReadJsonAsync(firstResponse);
            var completed = await WaitForFinalAsync(client, "branch", first.GetProperty("operationId").GetString()!);
            Assert.Equal("completed", completed.GetProperty("outcome").GetString());
        }
        finally
        {
            release.TrySetResult();
            sql.BackupBehavior = null;
        }
    }

    [Fact]
    public async Task ArbitraryDatabaseTargetIsRejectedWithoutExposingServerDetails()
    {
        ResetFakes();
        using var client = factory.CreateAdminClient();

        using var tokenResponse = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId = RmsDatabaseOperation.BackupOperationId, targetId = "RmsBranchSrv" });
        var tokenProblem = await ReadJsonAsync(tokenResponse);
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
        Assert.Equal(AgentProblemCodes.MutationTargetInvalid, tokenProblem.GetProperty("code").GetString());

        using var operationResponse = await client.PostAsJsonAsync(
            "/api/v1/rms/databases/RmsBranchSrv/backup",
            new { idempotencyKey = UniqueKey(), databaseName = "master", backupPath = "C:\\outside.bak" });
        var operationProblem = await ReadJsonAsync(operationResponse);
        var raw = operationProblem.GetRawText();
        Assert.Equal(HttpStatusCode.BadRequest, operationResponse.StatusCode);
        Assert.DoesNotContain("master", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outside.bak", raw, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonElement> RunBackupAsync(HttpClient client, string target)
    {
        var token = await IssueTokenAsync(client, RmsDatabaseOperation.BackupOperationId, target);
        using var response = await client.SendAsync(CreateBackupRequest(target, token, UniqueKey()));
        var initial = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await WaitForFinalAsync(client, target, initial.GetProperty("operationId").GetString()!);
    }

    private async Task<JsonElement> SendBackupAsync(HttpClient client, string target, string token, string key)
    {
        using var response = await client.SendAsync(CreateBackupRequest(target, token, key));
        return await ReadJsonAsync(response);
    }

    private async Task<JsonElement> SendRestoreAsync(
        HttpClient client,
        string target,
        string token,
        string artifactId,
        string confirmationText,
        string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/rms/databases/{Uri.EscapeDataString(target)}/restore")
        {
            Content = JsonContent.Create(new RmsDatabaseRestoreRequestDto(artifactId, confirmationText, key))
        };
        request.Headers.Add(MutationTokenContract.HeaderName, token);
        using var response = await client.SendAsync(request);
        var initial = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operationId = initial.GetProperty("operationId").GetString()!;
        return initial.GetProperty("state").GetString() == "notAttempted"
            ? initial
            : await WaitForFinalAsync(client, target, operationId);
    }

    private static HttpRequestMessage CreateBackupRequest(string target, string token, string key)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/rms/databases/{Uri.EscapeDataString(target)}/backup")
        {
            Content = JsonContent.Create(new RmsDatabaseBackupRequestDto(key))
        };
        request.Headers.Add(MutationTokenContract.HeaderName, token);
        return request;
    }

    private async Task<string> IssueTokenAsync(HttpClient client, string operationId, string target)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId, targetId = target });
        var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body.GetProperty("token").GetString()!;
    }

    private async Task<JsonElement> WaitForFinalAsync(HttpClient client, string target, string operationId)
    {
        JsonElement last = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var response = await client.GetAsync(
                $"/api/v1/rms/databases/{Uri.EscapeDataString(target)}/operations/{Uri.EscapeDataString(operationId)}");
            var body = await ReadJsonAsync(response);
            last = body;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var state = body.GetProperty("state").GetString();
            if (state is "completed" or "failed" or "outcomeUnknown" or "notAttempted")
            {
                return body;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"The synthetic RMS database operation did not reach a final state: {last.GetRawText()}");
    }

    private void ResetFakes()
    {
        var diagnostics = GetDiagnostics();
        diagnostics.Status = DomainRmsDatabaseDiagnosticStatus.Reachable;
        diagnostics.DatabaseNameMatches = true;
        diagnostics.ServerStatus = DomainRmsDatabaseDiagnosticStatus.Reachable;
        diagnostics.ServerDatabaseNameMatches = true;
        var sql = GetSql();
        sql.BackupOutcome = RmsDatabaseSqlOutcome.Completed;
        sql.RestoreOutcome = RmsDatabaseSqlOutcome.Completed;
        sql.VerifyResult = true;
        sql.BackupBehavior = null;
        sql.RestoreBehavior = null;
        while (sql.BackupCalls.TryDequeue(out _)) { }
        while (sql.RestoreCalls.TryDequeue(out _)) { }
        while (GetServiceManager().ControlCalls.TryDequeue(out _)) { }
    }

    private InMemoryRmsDatabaseDiagnostics GetDiagnostics() =>
        (InMemoryRmsDatabaseDiagnostics)factory.Services.GetRequiredService<IRmsDatabaseDiagnostics>();

    private InMemoryRmsDatabaseSqlOperations GetSql() =>
        (InMemoryRmsDatabaseSqlOperations)factory.Services.GetRequiredService<IRmsDatabaseSqlOperations>();

    private InMemoryServiceManager GetServiceManager() =>
        (InMemoryServiceManager)factory.Services.GetRequiredService<IServiceManager>();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    private static string UniqueKey() => $"database-{Guid.NewGuid():N}";
}
