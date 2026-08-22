using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public async Task WindowsServerIdentityVerifierAcceptsTheConnectedServerAndRejectsWrongIdentity()
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
        using var client = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(timeout.Token);
        await waitForConnection;

        var trustedVerifier = new WindowsLocalIpcServerIdentityVerifier(currentSid);
        var wrongVerifier = new WindowsLocalIpcServerIdentityVerifier(
            new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2999"));

        Assert.True(trustedVerifier.IsExpectedServer(client));
        Assert.False(wrongVerifier.IsExpectedServer(client));
    }

    [Fact]
    public void DefaultVerifierRequiresTheCurrentLocalSystemAgentIdentity()
    {
        var verifier = new WindowsLocalIpcServerIdentityVerifier();

        Assert.Equal(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            verifier.ExpectedServerSid);
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

        await Assert.ThrowsAsync<LocalIpcProtocolException>(() =>
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
}
