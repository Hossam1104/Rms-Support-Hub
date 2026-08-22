using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.LocalIpc;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class LocalIpcServerLifecycleRemediationTests
{
    [Fact]
    public async Task TransientPipeCreationFailureRecoversAndServesHealth()
    {
        var options = CreateOptions();
        var pipeFactory = new RecordingPipeFactory(failuresBeforeSuccess: 1);
        var (server, _) = CreateServer(options, pipeFactory);

        await server.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(
                () => pipeFactory.SuccessfulCreations > 0,
                TimeSpan.FromSeconds(5));

            var result = await new LocalIpcClient(options, new FixedIdentityVerifier(true))
                .GetHealthAsync("recovered-health");

            Assert.True(result.Succeeded);
            Assert.Equal("recovered-health", result.CorrelationId);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancellationDuringListenerBackoffStopsCleanly()
    {
        var options = CreateOptions();
        var pipeFactory = new RecordingPipeFactory(failuresBeforeSuccess: int.MaxValue);
        var (server, status) = CreateServer(options, pipeFactory);

        await server.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => pipeFactory.Attempts > 0, TimeSpan.FromSeconds(2));

        var started = DateTime.UtcNow;
        await server.StopAsync(CancellationToken.None);

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.Equal("disabled", status.GetHealth().IpcStatus);
    }

    [Fact]
    public async Task SmallConcurrencyBoundSurvivesRepeatedFullLoadCycles()
    {
        var options = CreateOptions(maxConcurrentClients: 2);
        var (server, _) = CreateServer(options, new RecordingPipeFactory());
        await server.StartAsync(CancellationToken.None);
        try
        {
            for (var cycle = 0; cycle < 5; cycle++)
            {
                var results = await Task.WhenAll(
                    Enumerable.Range(0, options.MaxConcurrentClients)
                        .Select(index => new LocalIpcClient(options, new FixedIdentityVerifier(true))
                            .GetHealthAsync($"cycle-{cycle}-{index}")));

                Assert.All(results, result => Assert.True(result.Succeeded));
                var health = await new LocalIpcClient(options, new FixedIdentityVerifier(true))
                    .GetHealthAsync($"after-cycle-{cycle}");
                Assert.True(health.Succeeded);
            }
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ShutdownWithAnActiveClientDoesNotDisposeSemaphoreBeforeRelease()
    {
        var options = CreateOptions();
        var (server, status) = CreateServer(options, new RecordingPipeFactory());
        await server.StartAsync(CancellationToken.None);
        using var client = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await client.ConnectAsync(timeout.Token);
            await WaitUntilAsync(() => status.GetHealth().IpcStatus == "ready", TimeSpan.FromSeconds(2));

            await server.StopAsync(timeout.Token);
            Assert.Equal("disabled", status.GetHealth().IpcStatus);
        }
        finally
        {
            client.Dispose();
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FirstPipeInstanceIsUsedOnlyForTheInitialOwnedInstance()
    {
        var options = CreateOptions(maxConcurrentClients: 2);
        var pipeFactory = new RecordingPipeFactory();
        var (server, _) = CreateServer(options, pipeFactory);
        await server.StartAsync(CancellationToken.None);
        try
        {
            var health = await new LocalIpcClient(options, new FixedIdentityVerifier(true))
                .GetHealthAsync("first-instance");
            Assert.True(health.Succeeded);
            await WaitUntilAsync(() => pipeFactory.Options.Count >= 2, TimeSpan.FromSeconds(2));

            var pipeOptions = pipeFactory.Options.ToArray();
            Assert.True(pipeOptions[0].HasFlag(PipeOptions.FirstPipeInstance));
            Assert.False(pipeOptions[1].HasFlag(PipeOptions.FirstPipeInstance));
            Assert.All(pipeFactory.MaxInstances, value =>
                Assert.Equal(NamedPipeServerStream.MaxAllowedServerInstances, value));
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ForeignNamespaceConflictDoesNotBypassFirstInstanceProtectionAndRecovers()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var options = CreateOptions();
        var foreignSecurity = new PipeSecurity();
        foreignSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreignSecurity.AddAccessRule(new PipeAccessRule(
            currentSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        using var foreignPipe = NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            foreignSecurity);
        var foreignWait = foreignPipe.WaitForConnectionAsync();
        var pipeFactory = new RecordingPipeFactory();
        var (server, status) = CreateServer(options, pipeFactory);

        await server.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => status.GetHealth().IpcStatus == "unavailable", TimeSpan.FromSeconds(2));
            Assert.Contains(pipeFactory.Options, value => value.HasFlag(PipeOptions.FirstPipeInstance));

            foreignPipe.Dispose();
            try { await foreignWait; } catch { }

            await WaitUntilAsync(() => pipeFactory.SuccessfulCreations > 0, TimeSpan.FromSeconds(5));
            var result = await new LocalIpcClient(options, new FixedIdentityVerifier(true))
                .GetHealthAsync("namespace-recovered");
            Assert.True(result.Succeeded);
        }
        finally
        {
            foreignPipe.Dispose();
            try { await foreignWait; } catch { }
            await server.StopAsync(CancellationToken.None);
        }
    }

    private static (LocalIpcServer Server, LocalIpcRuntimeStatus Status) CreateServer(
        LocalIpcOptions options,
        ILocalIpcServerPipeFactory pipeFactory)
    {
        var status = new LocalIpcRuntimeStatus();
        var server = new LocalIpcServer(
            options,
            new FixedOperatorGroupResolver(),
            new CurrentUserSecurityDescriptorFactory(),
            new TestInvocationContextFactory(),
            new RmsInstallationDiscoveryQueryHandler(new UnusedDiscovery(), new SuccessfulAuditSink(), TimeProvider.System),
            status,
            NullLogger<LocalIpcServer>.Instance,
            pipeFactory);
        return (server, status);
    }
    private static LocalIpcOptions CreateOptions(int maxConcurrentClients = 2) => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.Lifecycle." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        MaxRequestBytes = 64 * 1024,
        MaxResponseBytes = 256 * 1024,
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        ReadTimeout = TimeSpan.FromSeconds(5),
        MaxConcurrentClients = maxConcurrentClients
    };

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "The expected local IPC lifecycle state was not reached in time.");
    }

    private sealed class RecordingPipeFactory(int failuresBeforeSuccess = 0) : ILocalIpcServerPipeFactory
    {
        private int remainingFailures = failuresBeforeSuccess;
        private int attempts;
        private int successfulCreations;

        public int Attempts => Volatile.Read(ref attempts);

        public int SuccessfulCreations => Volatile.Read(ref successfulCreations);

        public ConcurrentQueue<PipeOptions> Options { get; } = [];

        public ConcurrentQueue<int> MaxInstances { get; } = [];

        public NamedPipeServerStream Create(
            string pipeName,
            PipeDirection direction,
            int maxNumberOfServerInstances,
            PipeTransmissionMode transmissionMode,
            PipeOptions options,
            int inBufferSize,
            int outBufferSize,
            PipeSecurity security)
        {
            Interlocked.Increment(ref attempts);
            Options.Enqueue(options);
            MaxInstances.Enqueue(maxNumberOfServerInstances);
            if (Interlocked.Decrement(ref remainingFailures) >= 0)
            {
                throw new IOException("simulated transient pipe creation failure");
            }

            Interlocked.Increment(ref successfulCreations);
            return new WindowsLocalIpcServerPipeFactory().Create(
                pipeName,
                direction,
                maxNumberOfServerInstances,
                transmissionMode,
                options,
                inBufferSize,
                outBufferSize,
                security);
        }
    }

    private sealed class FixedOperatorGroupResolver : ILocalIpcOperatorGroupResolver
    {
        public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
        {
            operatorGroupSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            return true;
        }
    }

    private sealed class CurrentUserSecurityDescriptorFactory : ILocalIpcSecurityDescriptorFactory
    {
        public PipeSecurity Create(SecurityIdentifier operatorGroupSid)
        {
            var currentSid = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new PipeAccessRule(currentSid, PipeAccessRights.FullControl, AccessControlType.Allow));
            return security;
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

    private sealed class SuccessfulAuditSink : IAgentAuditSink
    {
        public bool Record(AgentAuditEvent auditEvent) => true;
    }

    private sealed class UnusedDiscovery : IRmsInstallationDiscovery
    {
        public Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedIdentityVerifier(bool result) : ILocalIpcServerIdentityVerifier
    {
        public bool IsExpectedServer(NamedPipeClientStream pipe) => result;
    }
}
