using System.Net;
using System.Net.Http.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Pipes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.LocalIpc;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.LocalIpc;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class LocalIpcFoundationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void SecurityDescriptorContainsOnlyTheThreeAllowlistedPrincipals()
    {
        var operatorSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2001");
        var security = LocalIpcSecurityDescriptor.Create(operatorSid);
        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToArray();
        var sids = rules.Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value).ToHashSet();

        Assert.Equal(3, rules.Length);
        Assert.All(rules, rule => Assert.Equal(System.Security.AccessControl.AccessControlType.Allow, rule.AccessControlType));
        Assert.Equal(
            new HashSet<string>
            {
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
                operatorSid.Value
            },
            sids);
        Assert.DoesNotContain(new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value, sids);
        Assert.DoesNotContain(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Value, sids);
        Assert.DoesNotContain(new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null).Value, sids);
    }

    [Fact]
    public void MissingConfiguredOperatorGroupFailsClosedWithoutBroadFallback()
    {
        var resolver = new WindowsLocalIpcOperatorGroupResolver();

        var resolved = resolver.TryResolve(
            "RMS Support Operators That Cannot Exist In This Test",
            out _);

        Assert.False(resolved);
    }

    [Fact]
    public async Task WindowsNamedPipeRejectsCallerOutsideTheSecurityDescriptor()
    {
        var unauthorizedOperatorSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2002");
        var options = CreateOptions();
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            unauthorizedOperatorSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        using var server = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
        var waitForConnection = server.WaitForConnectionAsync();
        try
        {
            using var client = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => client.ConnectAsync(timeout.Token));
        }
        finally
        {
            server.Dispose();
            try
            {
                await waitForConnection;
            }
            catch
            {
                // Disposal is the bounded end of the server-side wait after the denied connection.
            }
        }
    }

    [Fact]
    public async Task NamedPipeClientExecutesHealthAndInstallationThroughTheSharedHandler()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var status = new LocalIpcRuntimeStatus();
        var handler = new RmsInstallationDiscoveryQueryHandler(
            new InMemoryRmsInstallationDiscovery(),
            new RecordingAuditSink(),
            TimeProvider.System);
        var server = new LocalIpcServer(
            options,
            new FixedOperatorGroupResolver(currentSid),
            new TestInvocationContextFactory(),
            handler,
            status,
            NullLogger<LocalIpcServer>.Instance);

        await server.StartAsync(CancellationToken.None);
        try
        {
            Assert.NotEqual("unavailable", status.GetHealth().IpcStatus);
            var client = new LocalIpcClient(options);
            var health = await client.GetHealthAsync("health-correlation");
            var installation = await client.GetInstallationDiscoveryAsync("diagnostic-correlation");

            Assert.True(health.Succeeded);
            Assert.Equal("ready", health.Result?.IpcStatus);
            Assert.False(health.Result?.HubConnectivityRequired);
            Assert.Equal(LocalIpcProtocol.CurrentVersion, health.Result?.ProtocolVersion);
            Assert.True(installation.Succeeded);
            Assert.Equal("BR-INT", installation.Result?.BranchCode);
            Assert.Equal("diagnostic-correlation", installation.CorrelationId);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnsupportedVersionUnknownOperationAndClientPrivilegePayloadFailClosed()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var server = CreateServer(options, currentSid);
        await server.StartAsync(CancellationToken.None);
        try
        {
            using var unsupported = await SendRawAsync(options, new LocalIpcRequestEnvelope(
                LocalIpcProtocol.CurrentVersion + 1,
                "request-version",
                "correlation-version",
                LocalIpcProtocol.HealthOperation,
                null));
            Assert.Equal("unsupported_protocol_version", unsupported.RootElement.GetProperty("error").GetProperty("code").GetString());

            using var unknown = await SendRawAsync(options, new LocalIpcRequestEnvelope(
                LocalIpcProtocol.CurrentVersion,
                "request-unknown",
                "correlation-unknown",
                "execute-anything",
                null));
            Assert.Equal("unknown_operation", unknown.RootElement.GetProperty("error").GetProperty("code").GetString());

            using var privilegePayload = await SendRawAsync(options, new LocalIpcRequestEnvelope(
                LocalIpcProtocol.CurrentVersion,
                "request-payload",
                "correlation-payload",
                LocalIpcProtocol.HealthOperation,
                JsonSerializer.SerializeToElement(new { isAdmin = true, role = "Administrator" })));
            Assert.True(privilegePayload.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("correlation-payload", privilegePayload.RootElement.GetProperty("correlationId").GetString());
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task MalformedAndOversizedRequestsAreRejectedWithoutTerminatingTheServer()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions(maxRequestBytes: 1024);
        var server = CreateServer(options, currentSid);
        await server.StartAsync(CancellationToken.None);
        try
        {
            using var malformed = await SendRawTextAsync(options, "not-json");
            Assert.Equal("malformed_request", malformed.RootElement.GetProperty("error").GetProperty("code").GetString());

            await Assert.ThrowsAnyAsync<Exception>(() =>
                SendRawTextAsync(options, "{\"padding\":\"" + new string('x', 2048) + "\"}"));

            var client = new LocalIpcClient(options);
            var health = await client.GetHealthAsync("after-invalid-request");
            Assert.True(health.Succeeded);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task LegacyHttpsInstallationRouteReturnsTheSameTypedResultAsIpc()
    {
        using var factory = new AgentWebApplicationFactory();
        using var http = factory.CreateAdminClient();
        using var response = await http.GetAsync("/api/v1/rms/installation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var httpBody = await response.Content.ReadAsStringAsync();
        var httpResult = JsonSerializer.Deserialize<RmsInstallationDto>(httpBody, JsonOptions);
        Assert.NotNull(httpResult);

        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var server = CreateServer(options, currentSid);
        await server.StartAsync(CancellationToken.None);
        try
        {
            var ipcResult = await new LocalIpcClient(options).GetInstallationDiscoveryAsync("parity-correlation");
            Assert.True(ipcResult.Succeeded);
            Assert.Equal(
                JsonSerializer.Serialize(httpResult, JsonOptions),
                JsonSerializer.Serialize(ipcResult.Result, JsonOptions));
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnabledIpcWithMissingOperatorGroupBecomesUnavailableAndDoesNotListen()
    {
        var options = CreateOptions();
        var status = new LocalIpcRuntimeStatus();
        var server = new LocalIpcServer(
            options,
            new MissingOperatorGroupResolver(),
            new TestInvocationContextFactory(),
            new RmsInstallationDiscoveryQueryHandler(
                new InMemoryRmsInstallationDiscovery(),
                new RecordingAuditSink(),
                TimeProvider.System),
            status,
            NullLogger<LocalIpcServer>.Instance);

        await server.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal("unavailable", status.GetHealth().IpcStatus);
            Assert.Equal("operator_group_unavailable", status.GetReason());
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    private static LocalIpcServer CreateServer(LocalIpcOptions options, SecurityIdentifier currentSid) =>
        new(
            options,
            new FixedOperatorGroupResolver(currentSid),
            new TestInvocationContextFactory(),
            new RmsInstallationDiscoveryQueryHandler(
                new InMemoryRmsInstallationDiscovery(),
                new RecordingAuditSink(),
                TimeProvider.System),
            new LocalIpcRuntimeStatus(),
            NullLogger<LocalIpcServer>.Instance);

    private static LocalIpcOptions CreateOptions(int maxRequestBytes = 64 * 1024) => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.Test." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        MaxRequestBytes = maxRequestBytes,
        MaxResponseBytes = 256 * 1024,
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        ReadTimeout = TimeSpan.FromSeconds(5),
        MaxConcurrentClients = 2
    };

    private static async Task<JsonDocument> SendRawAsync(
        LocalIpcOptions options,
        LocalIpcRequestEnvelope request)
    {
        return await SendRawTextAsync(options, JsonSerializer.Serialize(request));
    }

    private static async Task<JsonDocument> SendRawTextAsync(LocalIpcOptions options, string text)
    {
        using var pipe = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await pipe.ConnectAsync(timeout.Token);
        var bytes = Encoding.UTF8.GetBytes(text + "\n");
        await pipe.WriteAsync(bytes, timeout.Token);
        await pipe.FlushAsync(timeout.Token);
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        var response = await reader.ReadLineAsync(timeout.Token)
            ?? throw new InvalidOperationException("The IPC server closed without a bounded error response.");
        return JsonDocument.Parse(response);
    }

    private sealed class FixedOperatorGroupResolver(SecurityIdentifier sid) : ILocalIpcOperatorGroupResolver
    {
        public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
        {
            operatorGroupSid = sid;
            return true;
        }
    }

    private sealed class MissingOperatorGroupResolver : ILocalIpcOperatorGroupResolver
    {
        public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
        {
            operatorGroupSid = null!;
            return false;
        }
    }

    private sealed class TestInvocationContextFactory : IAgentInvocationContextFactory
    {
        public InvocationContext CreateLegacyLoopback(HttpContext context) => throw new NotSupportedException();

        public InvocationContext CreateLocalWpf(
            WindowsIdentity identity,
            SecurityIdentifier operatorGroupSid,
            string correlationId) => new(
                InvocationSource.LocalWpf,
                identity.User?.Value ?? "test-caller",
                InvocationAuthorizationLevel.LocalOperator,
                correlationId);
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        public void Record(AgentAuditEvent auditEvent)
        {
        }
    }
}
