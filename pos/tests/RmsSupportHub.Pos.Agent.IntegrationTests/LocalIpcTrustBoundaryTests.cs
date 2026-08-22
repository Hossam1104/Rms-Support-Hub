using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Contracts;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class LocalIpcTrustBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task WindowsServerIdentityVerifierAcceptsWhenPipePidMatchesCanonicalServicePid()
    {
        var (server, client) = await ConnectTestPipeAsync();
        using (server)
        using (client)
        {
            var pipeResolver = new WindowsLocalIpcPipeServerProcessIdResolver();
            Assert.True(pipeResolver.TryGetServerProcessId(client, out var pipeServerProcessId));
            var serviceResolver = new FixedServiceProcessResolver(ServiceOutcome.Running, pipeServerProcessId);

            var verifier = new WindowsLocalIpcServerIdentityVerifier(
                pipeResolver,
                serviceResolver);

            Assert.True(verifier.IsExpectedServer(client));
            Assert.Equal(AgentServiceIdentity.PermanentServiceName, serviceResolver.RequestedServiceName);
        }
    }

    [Fact]
    public async Task WindowsServerIdentityVerifierRejectsWhenPipePidDoesNotMatchCanonicalServicePid()
    {
        var (server, client) = await ConnectTestPipeAsync();
        using (server)
        using (client)
        {
            var pipeResolver = new WindowsLocalIpcPipeServerProcessIdResolver();
            Assert.True(pipeResolver.TryGetServerProcessId(client, out var pipeServerProcessId));
            var differentServiceProcessId = pipeServerProcessId == uint.MaxValue
                ? 1u
                : pipeServerProcessId + 1;

            var verifier = new WindowsLocalIpcServerIdentityVerifier(
                pipeResolver,
                new FixedServiceProcessResolver(ServiceOutcome.Running, differentServiceProcessId));

            Assert.False(verifier.IsExpectedServer(client));
        }
    }

    [Fact]
    public void DefaultVerifierUsesTheCanonicalServiceName()
    {
        var verifier = new WindowsLocalIpcServerIdentityVerifier();

        Assert.Equal(AgentServiceIdentity.PermanentServiceName, verifier.ExpectedServiceName);
        Assert.Equal("RmsSupportAgent", verifier.ExpectedServiceName);
    }

    [Fact]
    public Task WindowsServerIdentityVerifierRejectsWhenServiceIsNotFound() =>
        AssertVerifierRejectsAsync(new FixedServiceProcessResolver(ServiceOutcome.NotFound, 0));

    [Fact]
    public Task WindowsServerIdentityVerifierRejectsWhenServiceIsStopped() =>
        AssertVerifierRejectsAsync(new FixedServiceProcessResolver(ServiceOutcome.Stopped, 0));

    [Fact]
    public Task WindowsServerIdentityVerifierRejectsWhenServicePidIsUnavailable() =>
        AssertVerifierRejectsAsync(new FixedServiceProcessResolver(ServiceOutcome.Running, 0));

    [Fact]
    public Task WindowsServerIdentityVerifierRejectsWhenScmQueryFails() =>
        AssertVerifierRejectsAsync(new ThrowingServiceProcessResolver());

    [Fact]
    public Task WindowsServerIdentityVerifierRejectsWhenPipePidLookupFails() =>
        AssertVerifierRejectsAsync(
            new FixedServiceProcessResolver(ServiceOutcome.Running, 1234),
            new FixedPipeServerProcessIdResolver(false, 0));

    [Fact]
    public void WindowsServiceResolverRequestsOnlyScmConnectAndServiceQueryStatus()
    {
        Assert.Equal(0x0001u, WindowsLocalServiceProcessResolver.RequestedServiceManagerAccess);
        Assert.Equal(0x0004u, WindowsLocalServiceProcessResolver.RequestedServiceAccess);
        Assert.Equal(0u, WindowsLocalServiceProcessResolver.RequestedServiceManagerAccess & ~0x0001u);
        Assert.Equal(0u, WindowsLocalServiceProcessResolver.RequestedServiceAccess & ~0x0004u);
    }

    [Fact]
    public void WindowsServiceResolverResolvesTheRealEventLogService()
    {
        var resolver = new WindowsLocalServiceProcessResolver();

        Assert.True(resolver.TryGetRunningServiceProcessId("EventLog", out var processId));
        Assert.NotEqual(0u, processId);
    }

    [Fact]
    public void WindowsServiceResolverRejectsAUniqueNonexistentService()
    {
        var resolver = new WindowsLocalServiceProcessResolver();
        var serviceName = "RmsSupportHub.DoesNotExist." + Guid.NewGuid().ToString("N");

        Assert.False(resolver.TryGetRunningServiceProcessId(serviceName, out var processId));
        Assert.Equal(0u, processId);
    }

    [Fact]
    public void NativeImportsAreOwnedByTheActualResolverClassesAndRemainQueryOnly()
    {
        var importedEntryPoints = new[]
            {
                typeof(WindowsLocalServiceProcessResolver),
                typeof(WindowsLocalIpcPipeServerProcessIdResolver)
            }
            .SelectMany(type => type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => method.GetCustomAttribute<DllImportAttribute>()?.EntryPoint)
            .Where(entryPoint => entryPoint is not null)
            .Cast<string>()
            .ToArray();

        Assert.NotEmpty(importedEntryPoints);
        Assert.Contains("OpenSCManagerW", importedEntryPoints, StringComparer.Ordinal);
        Assert.Contains("OpenServiceW", importedEntryPoints, StringComparer.Ordinal);
        Assert.Contains("GetNamedPipeServerProcessId", importedEntryPoints, StringComparer.Ordinal);
        Assert.DoesNotContain("OpenProcess", importedEntryPoints, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenProcessToken", importedEntryPoints, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SeDebugPrivilege", importedEntryPoints, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductionOperatorAclSupportsRealPipePidVerification()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var security = LocalIpcSecurityDescriptor.Create(currentSid);
        using var server = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
        var waitForConnection = server.WaitForConnectionAsync();
        using var client = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await client.ConnectAsync(timeout.Token);
        await waitForConnection;

        var resolver = new WindowsLocalIpcPipeServerProcessIdResolver();
        Assert.True(resolver.TryGetServerProcessId(client, out var serverProcessId));
        Assert.NotEqual(0u, serverProcessId);
    }

    [Fact]
    public void LocalIpcClientRequestsIdentificationImpersonationOnly()
    {
        Assert.Equal(TokenImpersonationLevel.Identification, LocalIpcClient.RequestedImpersonationLevel);
    }

    [Fact]
    public async Task ClientRejectsIdentityLookupFailureBeforeSendingARequest()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        using var server = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            CreateTestPipeSecurity(currentSid));
        var waitForConnection = server.WaitForConnectionAsync();

        await Assert.ThrowsAsync<LocalIpcServerIdentityException>(() =>
            new LocalIpcClient(options, new FixedIdentityVerifier(false))
                .GetHealthAsync("trusted-correlation"));

        await waitForConnection;
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[1];
        Assert.Equal(0, await server.ReadAsync(buffer, readTimeout.Token));
    }

    [Fact]
    public async Task ClientRejectsResponseWithDifferentCorrelationId()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        using var server = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            CreateTestPipeSecurity(currentSid));
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
                "correlation-b",
                true,
                JsonSerializer.SerializeToElement(new LocalIpcHealthDto(
                    "ready",
                    "ready",
                    LocalIpcProtocol.CurrentVersion,
                    false), JsonOptions),
                null);
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
            await server.WriteAsync(responseBytes);
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });

        await Assert.ThrowsAsync<LocalIpcProtocolException>(() =>
            new LocalIpcClient(options, new FixedIdentityVerifier(true))
                .GetHealthAsync("correlation-a"));
        await serverTask;
    }

    private static LocalIpcOptions CreateOptions() => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.Trust." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        MaxRequestBytes = 64 * 1024,
        MaxResponseBytes = 256 * 1024,
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        ReadTimeout = TimeSpan.FromSeconds(5),
        MaxConcurrentClients = 1
    };

    private static async Task<(NamedPipeServerStream Server, NamedPipeClientStream Client)> ConnectTestPipeAsync()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var server = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            CreateTestPipeSecurity(currentSid));
        var waitForConnection = server.WaitForConnectionAsync();
        var client = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(timeout.Token);
            await waitForConnection;
            return (server, client);
        }
        catch
        {
            client.Dispose();
            server.Dispose();
            try
            {
                await waitForConnection;
            }
            catch
            {
                // Disposal bounds the server-side wait after setup failure.
            }

            throw;
        }
    }

    private static async Task AssertVerifierRejectsAsync(
        ILocalServiceProcessResolver serviceResolver,
        ILocalIpcPipeServerProcessIdResolver? pipeResolver = null)
    {
        var (server, client) = await ConnectTestPipeAsync();
        using (server)
        using (client)
        {
            var verifier = new WindowsLocalIpcServerIdentityVerifier(
                pipeResolver ?? new FixedPipeServerProcessIdResolver(true, 1234),
                serviceResolver);

            Assert.False(verifier.IsExpectedServer(client));
        }
    }

    private static PipeSecurity CreateTestPipeSecurity(SecurityIdentifier currentSid)
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(currentSid, PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }

    private sealed class FixedIdentityVerifier(bool result) : ILocalIpcServerIdentityVerifier
    {
        public bool IsExpectedServer(NamedPipeClientStream pipe) => result;
    }

    private sealed class FixedPipeServerProcessIdResolver(bool result, uint processId)
        : ILocalIpcPipeServerProcessIdResolver
    {
        public bool TryGetServerProcessId(NamedPipeClientStream pipe, out uint resolvedProcessId)
        {
            resolvedProcessId = processId;
            return result;
        }
    }

    private sealed class FixedServiceProcessResolver(ServiceOutcome result, uint processId)
        : ILocalServiceProcessResolver
    {
        public string? RequestedServiceName { get; private set; }

        public bool TryGetRunningServiceProcessId(string serviceName, out uint resolvedProcessId)
        {
            RequestedServiceName = serviceName;
            resolvedProcessId = processId;
            return result == ServiceOutcome.Running;
        }
    }

    private enum ServiceOutcome
    {
        NotFound,
        Stopped,
        Running
    }

    private sealed class ThrowingServiceProcessResolver : ILocalServiceProcessResolver
    {
        public bool TryGetRunningServiceProcessId(string serviceName, out uint processId) =>
            throw new InvalidOperationException("simulated SCM/query access failure");
    }
}
