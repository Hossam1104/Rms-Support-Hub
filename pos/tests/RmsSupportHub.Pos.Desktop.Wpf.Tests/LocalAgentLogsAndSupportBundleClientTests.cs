using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Support;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class LocalAgentLogsAndSupportBundleClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task LogEvidenceClientSendsNoPayloadAndMapsTheFixedServiceSet()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(
                await reader.ReadLineAsync() ?? throw new InvalidOperationException(),
                JsonOptions) ?? throw new InvalidOperationException();

            Assert.Equal(LocalIpcProtocol.LogEvidenceOperation, request.Operation);
            Assert.Null(request.Payload);
            var checkedAt = DateTimeOffset.UtcNow;
            var snapshot = new LogEvidenceSnapshotDto(
                checkedAt,
                LogEvidenceOverallState.Healthy,
                RmsServices().Select(service => new ServiceFailureAnalysisDto(
                    ToServiceId(service.ServiceName),
                    service.DisplayName,
                    FailureCategory.None,
                    FailureSeverity.Informational,
                    FailureConfidence.High,
                    "No bounded failure evidence was reported.",
                    checkedAt,
                    [],
                    [],
                    [])).ToArray());
            await WriteResponseAsync(server, request, snapshot);
        });

        var client = new LocalAgentLogEvidenceClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GetEvidenceAsync("logs-correlation");
        await serverTask;

        Assert.Equal(LogEvidenceViewState.Healthy, result.State);
        Assert.Equal(3, result.Services.Count);
        Assert.Equal("RMS Branch Service", result.Services[0].DisplayName);
        Assert.Equal("RMS Services Manager", result.Services[2].DisplayName);
    }

    [Fact]
    public async Task ContradictoryLogSnapshotFailsClosed()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(
                await reader.ReadLineAsync() ?? throw new InvalidOperationException(),
                JsonOptions) ?? throw new InvalidOperationException();
            var checkedAt = DateTimeOffset.UtcNow;
            var snapshot = new LogEvidenceSnapshotDto(
                checkedAt,
                LogEvidenceOverallState.Healthy,
                RmsServices().Select(service => new ServiceFailureAnalysisDto(
                    ToServiceId(service.ServiceName),
                    service.DisplayName,
                    FailureCategory.ServiceStopped,
                    FailureSeverity.ActionRequired,
                    FailureConfidence.High,
                    "A bounded failure was reported.",
                    checkedAt,
                    [],
                    [],
                    [])).ToArray());
            await WriteResponseAsync(server, request, snapshot);
        });

        var client = new LocalAgentLogEvidenceClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GetEvidenceAsync("logs-contradictory");
        await serverTask;

        Assert.Equal(LogEvidenceViewState.InvalidResponse, result.State);
        Assert.Equal("invalid_response", result.ErrorCode);
        Assert.DoesNotContain("password", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SupportBundleClientMapsOpaqueMetadataAndNeverNeedsAPath()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(
                await reader.ReadLineAsync() ?? throw new InvalidOperationException(),
                JsonOptions) ?? throw new InvalidOperationException();

            Assert.Equal(LocalIpcProtocol.SupportBundleOperation, request.Operation);
            Assert.Null(request.Payload);
            var created = DateTimeOffset.UtcNow;
            var bundle = new SupportBundleDto(
                new ArtifactMetadataDto(
                    "0123456789abcdef0123456789abcdef",
                    "rms-support-bundle.zip",
                    2048,
                    new string('a', 64),
                    created,
                    created.AddHours(1)),
                created,
                request.CorrelationId!,
                ["manifest", "health"]);
            await WriteResponseAsync(server, request, bundle);
        });

        var client = new LocalAgentSupportBundleClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GenerateAsync("bundle-correlation");
        await serverTask;

        Assert.Equal(SupportBundleViewState.Succeeded, result.State);
        Assert.NotNull(result.Artifact);
        Assert.Equal("rms-support-bundle.zip", result.Artifact!.DisplayName);
        Assert.DoesNotContain("path", result.Artifact.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.IncludedSections.Count);
    }

    [Fact]
    public async Task SupportBundleAuthorizationErrorMapsToSafeRetryState()
    {
        var options = CreateOptions();
        using var server = CreateServer(options);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(
                await reader.ReadLineAsync() ?? throw new InvalidOperationException(),
                JsonOptions) ?? throw new InvalidOperationException();
            var response = new LocalIpcResponseEnvelope(
                LocalIpcProtocol.CurrentVersion,
                request.RequestId,
                request.CorrelationId!,
                false,
                null,
                new LocalIpcErrorDto(
                    "administrator_authorization_required",
                    "native identity and password details"));
            await server.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
            await server.WriteAsync("\n"u8.ToArray());
            await server.FlushAsync();
        });

        var client = new LocalAgentSupportBundleClient(
            new LocalIpcClient(options, new FixedIdentityVerifier(true)));
        var result = await client.GenerateAsync("bundle-unauthorized");
        await serverTask;

        Assert.Equal(SupportBundleViewState.Unauthorized, result.State);
        Assert.Equal("administrator_authorization_required", result.ErrorCode);
        Assert.DoesNotContain("native", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<(string ServiceName, string DisplayName)> RmsServices() =>
    [
        ("RMS.BranchService", "RMS Branch Service"),
        ("RMS.CashierService", "RMS Cashier Service"),
        ("RMSServiceManager", "RMS Services Manager")
    ];

    private static string ToServiceId(string serviceName) =>
        "svc-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(serviceName)))
            .ToLowerInvariant()[..16];

    private static LocalIpcOptions CreateOptions() => new()
    {
        Enabled = true,
        PipeName = "RmsSupportAgent.WpfLogs." + Guid.NewGuid().ToString("N"),
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

    private sealed class FixedIdentityVerifier(bool expected) : ILocalIpcServerIdentityVerifier
    {
        public bool IsExpectedServer(NamedPipeClientStream pipe) => expected;
    }
}
