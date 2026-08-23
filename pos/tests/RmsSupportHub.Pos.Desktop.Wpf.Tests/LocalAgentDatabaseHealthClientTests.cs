using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class LocalAgentDatabaseHealthClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task TypedResponseMapsCanonicalRowsAndSendsNoCallerPayload()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("The client did not send a request.");
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(requestLine, JsonOptions)
                ?? throw new InvalidOperationException("The client request was malformed.");

            Assert.Equal(LocalIpcProtocol.DatabaseHealthOperation, request.Operation);
            Assert.Null(request.Payload);

            var snapshot = new RmsDatabaseHealthSnapshotDto(
                DateTimeOffset.UtcNow,
                RmsDatabaseHealthOverallState.Degraded,
                [
                    CreateItem(
                        RmsDatabaseTarget.Branch,
                        RmsDatabaseDiagnosticStatus.Reachable,
                        "Connected"),
                    CreateItem(
                        RmsDatabaseTarget.Cashier,
                        RmsDatabaseDiagnosticStatus.AuthenticationFailed,
                        "Authentication failed")
                ]);
            await WriteResponseAsync(server, request, snapshot);
        });

        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GetHealthAsync("database-correlation");
        await serverTask;

        Assert.Equal(DatabaseHealthViewState.Degraded, result.State);
        Assert.Equal("database-correlation", result.CorrelationId);
        Assert.Equal(2, result.Databases.Count);
        Assert.Equal("Connected", result.Databases[0].SafeDetail);
        Assert.Equal("Authentication failed", result.Databases[1].SafeDetail);
        Assert.DoesNotContain("native", result.Databases[0].SafeDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.Databases[1].SafeDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsafeSafeDetailIsRejectedBeforeItReachesTheWorkspace()
    {
        var item = CreateItem(
            RmsDatabaseTarget.Branch,
            RmsDatabaseDiagnosticStatus.Reachable,
            "SqlException password=secret at C:\\Rms\\stack trace");

        Assert.False(DatabaseHealthRow.TryCreate(item, out _));
    }

    [Fact]
    public async Task ErrorResponseMapsToFixedDatabaseHealthFailure()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("The client did not send a request.");
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(requestLine, JsonOptions)
                ?? throw new InvalidOperationException("The client request was malformed.");
            var response = new LocalIpcResponseEnvelope(
                LocalIpcProtocol.CurrentVersion,
                request.RequestId,
                request.CorrelationId!,
                false,
                null,
                new LocalIpcErrorDto(
                    "database_health_unavailable",
                    "SqlException password=secret at C:\\Windows\\System32\\config"));
            await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });

        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GetHealthAsync("database-unavailable");
        await serverTask;

        Assert.Equal(DatabaseHealthViewState.Unavailable, result.State);
        Assert.Equal("database_health_unavailable", result.ErrorCode);
        Assert.Equal("RMS database health is currently unavailable.", result.ErrorDetail);
        Assert.DoesNotContain("SqlException", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("database_health_timeout", DatabaseHealthViewState.TimedOut, "database_health_timeout")]
    [InlineData("protocol_mismatch", DatabaseHealthViewState.ProtocolMismatch, "protocol_mismatch")]
    [InlineData("security_verification_failed", DatabaseHealthViewState.SecurityVerificationFailed, "security_verification_failed")]
    [InlineData("invalid_response", DatabaseHealthViewState.InvalidResponse, "invalid_response")]
    [InlineData("agent_unavailable", DatabaseHealthViewState.Unavailable, "agent_unavailable")]
    public async Task FixedErrorCodesMapToDistinctSafeStates(
        string errorCode,
        DatabaseHealthViewState expectedState,
        string expectedCode)
    {
        var result = await GetErrorResultAsync(errorCode);

        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.DoesNotContain("native", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransportReadTimeoutMapsToTimedOutState()
    {
        var options = CreateOptions(readTimeout: TimeSpan.FromMilliseconds(100));
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            Assert.NotNull(await reader.ReadLineAsync());
            await Task.Delay(300);
        });
        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await client.GetHealthAsync("database-transport-timeout");
        await serverTask;

        Assert.Equal(DatabaseHealthViewState.TimedOut, result.State);
        Assert.Equal("database_health_timeout", result.ErrorCode);
    }

    [Fact]
    public async Task MalformedResponseMapsToInvalidResponse()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            Assert.NotNull(await reader.ReadLineAsync());
            await server.WriteAsync("not-json\n"u8.ToArray());
            await server.FlushAsync();
        });
        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await client.GetHealthAsync("database-malformed");
        await serverTask;

        Assert.Equal(DatabaseHealthViewState.InvalidResponse, result.State);
        Assert.Equal("invalid_response", result.ErrorCode);
        Assert.Equal("The Agent returned an invalid database health response.", result.ErrorDetail);
    }

    [Fact]
    public async Task ServerTrustFailureMapsToSecurityStateBeforeRequestWrite()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var waitForConnection = server.WaitForConnectionAsync();
        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(false)));

        var result = await client.GetHealthAsync("database-security");

        Assert.Equal(DatabaseHealthViewState.SecurityVerificationFailed, result.State);
        Assert.Equal("security_verification_failed", result.ErrorCode);
        await waitForConnection;
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[1];
        Assert.Equal(0, await server.ReadAsync(buffer, readTimeout.Token));
    }

    [Fact]
    public async Task ContradictoryReachableSnapshotIsRejected()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("The client did not send a request.");
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(requestLine, JsonOptions)
                ?? throw new InvalidOperationException("The client request was malformed.");
            var invalidItem = CreateItem(
                RmsDatabaseTarget.Branch,
                RmsDatabaseDiagnosticStatus.Reachable,
                "Connected",
                configured: false,
                configuredDatabase: null,
                serverDisplay: null,
                nameMatches: null);
            var snapshot = new RmsDatabaseHealthSnapshotDto(
                DateTimeOffset.UtcNow,
                RmsDatabaseHealthOverallState.Healthy,
                [
                    invalidItem,
                    CreateItem(RmsDatabaseTarget.Cashier, RmsDatabaseDiagnosticStatus.Reachable)
                ]);
            await WriteResponseAsync(server, request, snapshot);
        });

        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GetHealthAsync("database-invalid");
        await serverTask;

        Assert.Equal(DatabaseHealthViewState.InvalidResponse, result.State);
        Assert.Equal("invalid_response", result.ErrorCode);
        Assert.DoesNotContain("configured", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContractSerializationContainsNoCredentialOrSqlFields()
    {
        var snapshot = new RmsDatabaseHealthSnapshotDto(
            DateTimeOffset.UtcNow,
            RmsDatabaseHealthOverallState.Healthy,
            [CreateItem(RmsDatabaseTarget.Branch, RmsDatabaseDiagnosticStatus.Reachable)]);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        Assert.DoesNotContain("\"connectionString\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"password\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"userId\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"sql\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"query\"", json, StringComparison.OrdinalIgnoreCase);
    }

    private static RmsDatabaseHealthItemDto CreateItem(
        RmsDatabaseTarget database,
        RmsDatabaseDiagnosticStatus status,
        string safeDetail = "Connected",
        bool configured = true,
        string? configuredDatabase = null,
        string? serverDisplay = null,
        bool? nameMatches = true)
    {
        var expected = database == RmsDatabaseTarget.Branch ? "RmsBranchSrv" : "RmsCashierSrv";
        var displayName = database == RmsDatabaseTarget.Branch ? "Branch Database" : "Cashier Database";
        return new(
            database,
            displayName,
            expected,
            configuredDatabase ?? expected,
            serverDisplay ?? "integration-sql:1433",
            configured,
            nameMatches,
            status,
            safeDetail,
            DateTimeOffset.UtcNow);
    }

    private static async Task WriteResponseAsync<T>(
        NamedPipeServerStream server,
        LocalIpcRequestEnvelope request,
        T result)
    {
        var response = new LocalIpcResponseEnvelope(
            LocalIpcProtocol.CurrentVersion,
            request.RequestId,
            request.CorrelationId!,
            true,
            JsonSerializer.SerializeToElement(result, JsonOptions),
            null);
        await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
        await server.WriteAsync("\n"u8.ToArray());
        await server.FlushAsync();
    }

    private static async Task<DatabaseHealthResult> GetErrorResultAsync(string errorCode)
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync()
                ?? throw new InvalidOperationException("The client did not send a request.");
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(requestLine, JsonOptions)
                ?? throw new InvalidOperationException("The client request was malformed.");
            var response = new LocalIpcResponseEnvelope(
                LocalIpcProtocol.CurrentVersion,
                request.RequestId,
                request.CorrelationId!,
                false,
                null,
                new LocalIpcErrorDto(errorCode, "native exception password=secret"));
            await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });
        var client = new LocalAgentDatabaseHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await client.GetHealthAsync("database-error-code");
        await serverTask;
        return result;
    }

    private static LocalIpcOptions CreateOptions(TimeSpan? readTimeout = null) => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.WpfDatabaseHealth." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        ReadTimeout = readTimeout ?? TimeSpan.FromSeconds(5),
        MaxConcurrentClients = 1
    };

    private static NamedPipeServerStream CreateServer(LocalIpcOptions options)
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            currentSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private sealed class FixedIdentityVerifier(bool expected) : ILocalIpcServerIdentityVerifier
    {
        public bool IsExpectedServer(NamedPipeClientStream pipe) => expected;
    }
}
