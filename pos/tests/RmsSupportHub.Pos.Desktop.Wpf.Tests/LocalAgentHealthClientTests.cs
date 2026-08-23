using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class LocalAgentHealthClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

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

    [Fact]
    public async Task ServiceHealthResponseMapsToTypedRowsAndPreservesCorrelation()
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
            var snapshot = new ServiceHealthSnapshotDto(
                ServiceHealthOverallState.Degraded,
                DateTimeOffset.UtcNow,
                [
                    new ServiceHealthItemDto(
                        "svc-branch",
                        "RMS Branch Service",
                        true,
                        true,
                        ServiceRuntimeState.Running,
                        "running"),
                    new ServiceHealthItemDto(
                        "svc-cashier",
                        "RMS Cashier Service",
                        true,
                        false,
                        ServiceRuntimeState.NotFound,
                        "not_installed")
                ]);
            var response = new LocalIpcResponseEnvelope(
                LocalIpcProtocol.CurrentVersion,
                request.RequestId,
                request.CorrelationId!,
                true,
                JsonSerializer.SerializeToElement(snapshot, JsonOptions),
                null);
            await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });
        var healthClient = new LocalAgentServiceHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await healthClient.GetHealthAsync("service-correlation");
        await serverTask;

        Assert.Equal(ServiceHealthViewState.Degraded, result.State);
        Assert.Equal("service-correlation", result.CorrelationId);
        Assert.Equal(2, result.Services.Count);
        Assert.Equal(1, result.RunningCount);
        Assert.Equal(1, result.NotInstalledCount);
        Assert.Equal("Not installed", result.Services[1].RuntimeLabel);
        Assert.Equal("Not installed", result.Services[1].InstallationLabel);
    }

    [Fact]
    public async Task ServiceHealthErrorResponseMapsToBoundedTimeoutState()
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
                new LocalIpcErrorDto("service_health_timeout", "native exception details"));
            await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });
        var healthClient = new LocalAgentServiceHealthClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));

        var result = await healthClient.GetHealthAsync("service-timeout");
        await serverTask;

        Assert.Equal(ServiceHealthViewState.TimedOut, result.State);
        Assert.Equal("service_health_timeout", result.ErrorCode);
        Assert.Equal("Service health check timed out.", result.ErrorDetail);
        Assert.DoesNotContain("native", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
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
