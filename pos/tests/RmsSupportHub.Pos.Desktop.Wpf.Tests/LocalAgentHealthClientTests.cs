using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class LocalAgentHealthClientTests
{
    [Fact]
    public async Task ServerTrustFailureMapsSafelyAndSendsNoRequestBytes()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var waitForConnection = server.WaitForConnectionAsync();
        var healthClient = new LocalAgentHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(false)));

        var result = await healthClient.GetHealthAsync("trust-correlation");

        Assert.Equal(HealthViewState.SecurityVerificationFailed, result.State);
        Assert.Equal("security_verification_failed", result.ErrorCode);
        Assert.Equal("The local Agent connection could not be verified.", result.ErrorDetail);
        await waitForConnection;

        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[1];
        Assert.Equal(0, await server.ReadAsync(buffer, readTimeout.Token));
    }

    [Fact]
    public async Task MalformedResponseRemainsAnInvalidResponse()
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
        var healthClient = new LocalAgentHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await healthClient.GetHealthAsync("malformed-correlation");
        await serverTask;

        Assert.Equal(HealthViewState.InvalidResponse, result.State);
        Assert.Equal("invalid_response", result.ErrorCode);
        Assert.Equal("The Agent returned an invalid health response.", result.ErrorDetail);
    }

    private static LocalIpcOptions CreateOptions() => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.WpfHealth." + Guid.NewGuid().ToString("N"),
        OperatorGroupName = "test-operator-group",
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        ReadTimeout = TimeSpan.FromSeconds(5),
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
