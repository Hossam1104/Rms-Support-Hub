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
    public void SecurityDescriptorContainsOnlyTheThreeAllowlistedPrincipalsAndExplicitNetworkDeny()
    {
        var operatorSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2001");
        var security = LocalIpcSecurityDescriptor.Create(operatorSid);
        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToArray();
        var sids = rules.Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value).ToHashSet();

        Assert.Equal(4, rules.Length);
        Assert.Equal(3, rules.Count(rule => rule.AccessControlType == AccessControlType.Allow));
        var networkRule = Assert.Single(rules, rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value ==
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null).Value);
        Assert.Equal(AccessControlType.Deny, networkRule.AccessControlType);
        Assert.Equal(PipeAccessRights.FullControl, networkRule.PipeAccessRights);
        Assert.DoesNotContain(rules, rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value ==
            new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value);
        Assert.Equal(
            new HashSet<string>
            {
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
                operatorSid.Value
            },
            rules.Where(rule => rule.AccessControlType == AccessControlType.Allow)
                .Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value)
                .ToHashSet());
        Assert.DoesNotContain(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Value, sids);
        Assert.DoesNotContain(new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null).Value, sids);

        var systemRule = Assert.Single(rules, rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value ==
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value);
        Assert.Equal(AccessControlType.Allow, systemRule.AccessControlType);
        var administratorRule = Assert.Single(rules, rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value ==
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value);
        Assert.Equal(AccessControlType.Allow, administratorRule.AccessControlType);
    }

    [Fact]
    public void OperatorAceUsesOnlyDuplexClientRightsAndCannotChangePipeSecurity()
    {
        var operatorSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2001");
        var security = LocalIpcSecurityDescriptor.Create(operatorSid);
        var rule = Assert.Single(
            security
                .GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>(),
            candidate => ((SecurityIdentifier)candidate.IdentityReference).Equals(operatorSid));

        var expected = PipeAccessRights.ReadData
            | PipeAccessRights.WriteData
            | PipeAccessRights.ReadExtendedAttributes
            | PipeAccessRights.WriteExtendedAttributes
            | PipeAccessRights.ReadAttributes
            | PipeAccessRights.WriteAttributes
            | PipeAccessRights.ReadPermissions
            | PipeAccessRights.Synchronize;
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(expected, rule.PipeAccessRights);
        var forbidden = new[]
        {
            PipeAccessRights.ChangePermissions,
            PipeAccessRights.TakeOwnership,
            PipeAccessRights.AccessSystemSecurity,
            PipeAccessRights.CreateNewInstance,
            PipeAccessRights.Delete
        };
        Assert.All(forbidden, rights => Assert.False(rule.PipeAccessRights.HasFlag(rights)));
    }

    [Fact]
    public void GroupResolverAlwaysQualifiesUnqualifiedNamesToTheCurrentMachine()
    {
        var localSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2101");
        var bareCollisionSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2102");
        var resolver = new WindowsLocalIpcOperatorGroupResolver(
            new RecordingAccountSidResolver(
                new Dictionary<string, SecurityIdentifier>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{Environment.MachineName}\\RMS Support Operators"] = localSid,
                    ["DOMAIN\\RMS Support Operators"] = bareCollisionSid
                }),
            new FixedAccountTypeResolver(LocalIpcAccountType.Alias));

        Assert.True(resolver.TryResolve("RMS Support Operators", out var resolved));
        Assert.Equal(localSid, resolved);
    }

    [Fact]
    public void GroupResolverAcceptsOnlyExplicitCurrentMachineQualification()
    {
        var localSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2103");
        var resolver = new WindowsLocalIpcOperatorGroupResolver(
            new RecordingAccountSidResolver(
                new Dictionary<string, SecurityIdentifier>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{Environment.MachineName}\\RMS Support Operators"] = localSid
                }),
            new FixedAccountTypeResolver(LocalIpcAccountType.Group));


        Assert.True(resolver.TryResolve($"{Environment.MachineName}\\RMS Support Operators", out var resolved));
        Assert.Equal(localSid, resolved);
        Assert.False(resolver.TryResolve("DOMAIN\\RMS Support Operators", out _));
        Assert.False(resolver.TryResolve("FOREIGN-MACHINE\\RMS Support Operators", out _));
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

    [Theory]
    [InlineData(LocalIpcAccountType.Alias, true)]
    [InlineData(LocalIpcAccountType.Group, true)]
    [InlineData(LocalIpcAccountType.User, false)]
    [InlineData(LocalIpcAccountType.Computer, false)]
    [InlineData(LocalIpcAccountType.Domain, false)]
    [InlineData(LocalIpcAccountType.WellKnownGroup, false)]
    [InlineData(LocalIpcAccountType.DeletedAccount, false)]
    [InlineData(LocalIpcAccountType.Unknown, false)]
    public void OperatorGroupResolverEnforcesLocalGroupAccountType(LocalIpcAccountType accountType, bool expected)
    {
        var sid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2199");
        var resolver = new WindowsLocalIpcOperatorGroupResolver(
            new RecordingAccountSidResolver(new Dictionary<string, SecurityIdentifier>(StringComparer.OrdinalIgnoreCase)
            {
                [$"{Environment.MachineName}\\RMS Support Operators"] = sid
            }),
            new FixedAccountTypeResolver(accountType));

        Assert.Equal(expected, resolver.TryResolve("RMS Support Operators", out _));
    }

    [Theory]
    [InlineData(WellKnownSidType.WorldSid)]
    [InlineData(WellKnownSidType.AuthenticatedUserSid)]
    [InlineData(WellKnownSidType.BuiltinUsersSid)]
    [InlineData(WellKnownSidType.BuiltinGuestsSid)]
    [InlineData(WellKnownSidType.AnonymousSid)]
    [InlineData(WellKnownSidType.InteractiveSid)]
    public void OperatorGroupResolverRejectsBroadSids(WellKnownSidType broadSidType)
    {
        var broadSid = new SecurityIdentifier(broadSidType, null);
        var resolver = new WindowsLocalIpcOperatorGroupResolver(
            new RecordingAccountSidResolver(new Dictionary<string, SecurityIdentifier>(StringComparer.OrdinalIgnoreCase)
            {
                [$"{Environment.MachineName}\\RMS Support Operators"] = broadSid
            }),
            new FixedAccountTypeResolver(LocalIpcAccountType.Alias));

        Assert.False(resolver.TryResolve("RMS Support Operators", out _));
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
            new FixedOperatorGroupResolver(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)),
            new TestSecurityDescriptorFactory(),
            new TestInvocationContextFactory(),
            handler,
            status,
            NullLogger<LocalIpcServer>.Instance);

        await server.StartAsync(CancellationToken.None);
        try
        {
            Assert.NotEqual("unavailable", status.GetHealth().IpcStatus);
            var client = new LocalIpcClient(options, new FixedIdentityVerifier(true));
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
    public async Task NullCorrelationFallsBackToRequestIdInResponseAndAudit()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var audit = new RecordingAuditSink();
        var server = CreateServer(options, currentSid, audit);
        await server.StartAsync(CancellationToken.None);
        try
        {
            using var response = await SendRawAsync(options, new LocalIpcRequestEnvelope(
                LocalIpcProtocol.CurrentVersion,
                "request-null-correlation",
                null,
                LocalIpcProtocol.InstallationDiscoveryOperation,
                null));

            Assert.True(response.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(
                "request-null-correlation",
                response.RootElement.GetProperty("correlationId").GetString());
            Assert.Equal("request-null-correlation", Assert.Single(audit.Events).CorrelationId);
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

            var client = new LocalIpcClient(options, new FixedIdentityVerifier(true));
            var health = await client.GetHealthAsync("after-invalid-request");
            Assert.True(health.Succeeded);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RequestReaderAcceptsMaximumContentAndRejectsOnlyTheNextByte()
    {
        var maximumBytes = 1024;
        foreach (var length in new[] { maximumBytes - 1, maximumBytes, maximumBytes + 1 })
        {
            var currentSid = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
            var options = CreateOptions(
                maxRequestBytes: maximumBytes,
                pipeName: "RmsSupportAgent.RequestBounds." + Guid.NewGuid().ToString("N"));
            var server = CreateServer(options, currentSid);
            await server.StartAsync(CancellationToken.None);
            try
            {
                var request = CreatePaddedRequest(length);
                Assert.Equal(length, Encoding.UTF8.GetByteCount(request));
                if (length <= maximumBytes)
                {
                    using var response = await SendRawTextAsync(options, request);
                    Assert.True(response.RootElement.GetProperty("success").GetBoolean());
                }
                else
                {
                    await Assert.ThrowsAnyAsync<Exception>(() => SendRawTextAsync(options, request));
                }
            }
            finally
            {
                await server.StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task ResponseReaderAcceptsMaximumContentAndRejectsOnlyTheNextByte()
    {
        var maximumBytes = 1024;
        foreach (var length in new[] { maximumBytes - 1, maximumBytes, maximumBytes + 1 })
        {
            var currentSid = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
            var options = CreateOptions(
                maxResponseBytes: maximumBytes,
                pipeName: "RmsSupportAgent.ResponseBounds." + Guid.NewGuid().ToString("N"));
            var security = new TestSecurityDescriptorFactory().Create(currentSid);
            using var server = NamedPipeServerStreamAcl.Create(
                options.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                security);
            var serverTask = Task.Run(async () =>
            {
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync()
                    ?? throw new InvalidOperationException("The client request was missing.");
                var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(requestLine, JsonOptions)
                    ?? throw new InvalidOperationException("The client request was malformed.");
                var response = CreatePaddedResponse(request, length);
                if (Encoding.UTF8.GetByteCount(response) != length)
                {
                    throw new InvalidOperationException($"Response boundary fixture was {Encoding.UTF8.GetByteCount(response)} bytes, expected {length}.");
                }
                await server.WriteAsync(Encoding.UTF8.GetBytes(response + "\n"));
                await server.FlushAsync();
            });

            var client = new LocalIpcClient(options, new FixedIdentityVerifier(true));
            if (length <= maximumBytes)
            {
                var result = await client.GetHealthAsync("response-bound");
                Assert.True(result.Succeeded);
            }
            else
            {
                await Assert.ThrowsAsync<LocalIpcProtocolException>(() => client.GetHealthAsync("response-bound"));
            }

            if (length <= maximumBytes)
            {
                await serverTask;
            }
            else
            {
                await Assert.ThrowsAnyAsync<Exception>(() => serverTask);
            }
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
            var ipcResult = await new LocalIpcClient(options, new FixedIdentityVerifier(true))
                .GetInstallationDiscoveryAsync("parity-correlation");
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
            new TestSecurityDescriptorFactory(),
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

    private static LocalIpcServer CreateServer(
        LocalIpcOptions options,
        SecurityIdentifier currentSid,
        RecordingAuditSink? audit = null) =>
        new(
            options,
            new FixedOperatorGroupResolver(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)),
            new TestSecurityDescriptorFactory(),
            new TestInvocationContextFactory(),
            new RmsInstallationDiscoveryQueryHandler(
                new InMemoryRmsInstallationDiscovery(),
                audit ?? new RecordingAuditSink(),
                TimeProvider.System),
            new LocalIpcRuntimeStatus(),
            NullLogger<LocalIpcServer>.Instance);

    private static LocalIpcOptions CreateOptions(
        int maxRequestBytes = 64 * 1024,
        int maxResponseBytes = 256 * 1024,
        string? pipeName = null) => new()
    {
        Enabled = true,
        PipeName = pipeName ?? "RmsSupportAgent.Test." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        MaxRequestBytes = maxRequestBytes,
        MaxResponseBytes = maxResponseBytes,
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

    private static string CreatePaddedRequest(int targetBytes)
    {
        var request = new LocalIpcRequestEnvelope(
            LocalIpcProtocol.CurrentVersion,
            "request-boundary",
            "correlation-boundary",
            LocalIpcProtocol.HealthOperation,
            JsonSerializer.SerializeToElement(new { padding = string.Empty }));
        var baseJson = JsonSerializer.Serialize(request, JsonOptions);
        var paddingLength = Math.Max(0, targetBytes - Encoding.UTF8.GetByteCount(baseJson));
        while (true)
        {
            var candidate = JsonSerializer.Serialize(
                request with { Payload = JsonSerializer.SerializeToElement(new { padding = new string('x', paddingLength) }) },
                JsonOptions);
            var difference = targetBytes - Encoding.UTF8.GetByteCount(candidate);
            if (difference == 0)
            {
                return candidate;
            }

            paddingLength += difference;
        }
    }

    private static string CreatePaddedResponse(LocalIpcRequestEnvelope request, int targetBytes)
    {
        var paddingLength = Math.Max(0, targetBytes - 256);
        while (true)
        {
            var candidate = JsonSerializer.Serialize(new
            {
                protocolVersion = LocalIpcProtocol.CurrentVersion,
                requestId = request.RequestId,
                correlationId = request.CorrelationId,
                success = true,
                result = new LocalIpcHealthDto("ready", "ready", LocalIpcProtocol.CurrentVersion, false),
                error = (object?)null,
                padding = new string('x', paddingLength)
            },
                JsonOptions);
            var difference = targetBytes - Encoding.UTF8.GetByteCount(candidate);
            if (difference == 0)
            {
                return candidate;
            }

            paddingLength += difference;
        }
    }

    private sealed class FixedOperatorGroupResolver(SecurityIdentifier sid) : ILocalIpcOperatorGroupResolver
    {
        public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
        {
            operatorGroupSid = sid;
            return true;
        }
    }

    private sealed class RecordingAccountSidResolver(
        IReadOnlyDictionary<string, SecurityIdentifier> accounts) : ILocalIpcAccountSidResolver
    {
        public bool TryResolve(string accountName, out SecurityIdentifier sid) =>
            accounts.TryGetValue(accountName, out sid!);
    }

    private sealed class FixedAccountTypeResolver(LocalIpcAccountType accountType) : ILocalIpcAccountTypeResolver
    {
        public bool TryResolve(SecurityIdentifier sid, out LocalIpcAccountType resolvedType)
        {
            resolvedType = accountType;
            return true;
        }
    }

    private sealed class TestSecurityDescriptorFactory : ILocalIpcSecurityDescriptorFactory
    {
        public PipeSecurity Create(SecurityIdentifier operatorGroupSid)
        {
            var currentSid = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new PipeAccessRule(
                currentSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            return security;
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

    private sealed class FixedIdentityVerifier(bool result) : ILocalIpcServerIdentityVerifier
    {
        public bool IsExpectedServer(NamedPipeClientStream pipe) => result;
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
        public List<AgentAuditEvent> Events { get; } = [];

        public bool Record(AgentAuditEvent auditEvent)
        {
            Events.Add(auditEvent);
            return true;
        }
    }
}
