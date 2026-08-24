using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using System.Text.Json.Serialization;
using System.Security.Principal;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Support;

namespace RmsSupportHub.Pos.LocalIpc;

public sealed record LocalIpcCallResult<T>(
    bool Succeeded,
    T? Result,
    string RequestId,
    string CorrelationId,
    string? ErrorCode,
    string? ErrorMessage);

public class LocalIpcProtocolException(string message) : Exception(message);

public sealed class LocalIpcProtocolMismatchException(int expectedVersion, int actualVersion)
    : LocalIpcProtocolException("The IPC response used an incompatible protocol version.")
{
    public int ExpectedVersion { get; } = expectedVersion;

    public int ActualVersion { get; } = actualVersion;
}

public sealed class LocalIpcServerIdentityException()
    : LocalIpcProtocolException("The IPC server identity could not be verified.");

/// <summary>
/// Small typed client for the WPF-to-Agent local IPC contract. It exposes only the operations
/// implemented by the accepted WPF slices; arbitrary operation names cannot be supplied by callers.
/// </summary>
public sealed class LocalIpcClient
{
    /// <summary>
    /// WPF-01's normal local IPC posture. Only the typed artifact export operation is allowed
    /// to request the stronger destination-write token posture below.
    /// </summary>
    public const TokenImpersonationLevel RequestedImpersonationLevel = TokenImpersonationLevel.Identification;

    public const TokenImpersonationLevel ArtifactExportRequestedImpersonationLevel =
        TokenImpersonationLevel.Impersonation;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly LocalIpcOptions options;
    private readonly ILocalIpcServerIdentityVerifier serverIdentityVerifier;

    public LocalIpcClient(
        LocalIpcOptions? options = null,
        ILocalIpcServerIdentityVerifier? serverIdentityVerifier = null)
    {
        this.options = options ?? new LocalIpcOptions();
        this.options.Validate();
        this.serverIdentityVerifier = serverIdentityVerifier ?? new WindowsLocalIpcServerIdentityVerifier();
    }

    public Task<LocalIpcCallResult<LocalIpcHealthDto>> GetHealthAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<LocalIpcHealthDto>(LocalIpcProtocol.HealthOperation, correlationId, cancellationToken);

    public Task<LocalIpcCallResult<RmsInstallationDto>> GetInstallationDiscoveryAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<RmsInstallationDto>(
            LocalIpcProtocol.InstallationDiscoveryOperation,
            correlationId,
            cancellationToken);

    public Task<LocalIpcCallResult<ServiceHealthSnapshotDto>> GetServiceHealthAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<ServiceHealthSnapshotDto>(
            LocalIpcProtocol.ServiceHealthOperation,
            correlationId,
            cancellationToken);

    public Task<LocalIpcCallResult<RmsDatabaseHealthSnapshotDto>> GetDatabaseHealthAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<RmsDatabaseHealthSnapshotDto>(
            LocalIpcProtocol.DatabaseHealthOperation,
            correlationId,
            cancellationToken);

    public Task<LocalIpcCallResult<LogEvidenceSnapshotDto>> GetLogEvidenceAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<LogEvidenceSnapshotDto>(
            LocalIpcProtocol.LogEvidenceOperation,
            correlationId,
            cancellationToken);

    public Task<LocalIpcCallResult<SupportBundleDto>> GenerateSupportBundleAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<SupportBundleDto>(
            LocalIpcProtocol.SupportBundleOperation,
            correlationId,
            cancellationToken);

    public Task<LocalIpcCallResult<LocalIpcAuthorizationDto>> GetAuthorizationAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<LocalIpcAuthorizationDto>(
            LocalIpcProtocol.AuthorizationOperation,
            correlationId,
            payload: null,
            cancellationToken);

    public Task<LocalIpcCallResult<LocalIpcBackupInventoryDto>> GetBackupInventoryAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<LocalIpcBackupInventoryDto>(
            LocalIpcProtocol.BackupInventoryOperation,
            correlationId,
            payload: null,
            cancellationToken);

    public Task<LocalIpcCallResult<RmsDatabaseOperationDto>> CreateBackupAsync(
        RmsDatabaseTarget target,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<RmsDatabaseOperationDto>(
            LocalIpcProtocol.BackupCreateOperation,
            correlationId,
            new LocalIpcBackupCreateRequestDto(target),
            cancellationToken);

    public Task<LocalIpcCallResult<RmsDatabaseOperationDto>> GetBackupStatusAsync(
        RmsDatabaseTarget target,
        string operationId,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<RmsDatabaseOperationDto>(
            LocalIpcProtocol.BackupStatusOperation,
            correlationId,
            new LocalIpcBackupOperationRequestDto(target, operationId),
            cancellationToken);

    public Task<LocalIpcCallResult<RmsDatabaseOperationDto>> CancelBackupAsync(
        RmsDatabaseTarget target,
        string operationId,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<RmsDatabaseOperationDto>(
            LocalIpcProtocol.BackupCancelOperation,
            correlationId,
            new LocalIpcBackupOperationRequestDto(target, operationId),
            cancellationToken);

    public Task<LocalIpcCallResult<LocalIpcArtifactExportResultDto>> ExportArtifactAsync(
        LocalIpcArtifactExportRequestDto request,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<LocalIpcArtifactExportResultDto>(
            LocalIpcProtocol.ArtifactExportOperation,
            correlationId,
            request,
            cancellationToken,
            ArtifactExportRequestedImpersonationLevel);

    private async Task<LocalIpcCallResult<T>> SendAsync<T>(
        string operation,
        string? correlationId,
        CancellationToken cancellationToken) =>
        await SendAsync<T>(
            operation,
            correlationId,
            payload: null,
            cancellationToken,
            RequestedImpersonationLevel).ConfigureAwait(false);

    private async Task<LocalIpcCallResult<T>> SendAsync<T>(
        string operation,
        string? correlationId,
        object? payload,
        CancellationToken cancellationToken,
        TokenImpersonationLevel impersonationLevel = RequestedImpersonationLevel)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var effectiveCorrelationId = IsSafeToken(correlationId) ? correlationId! : requestId;
        var request = new LocalIpcRequestEnvelope(
            LocalIpcProtocol.CurrentVersion,
            requestId,
            effectiveCorrelationId,
            operation,
            payload is null
                ? null
                : JsonSerializer.SerializeToElement(payload, JsonOptions));
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        if (requestBytes.Length > options.MaxRequestBytes)
        {
            throw new LocalIpcProtocolException("The IPC request exceeds the configured size limit.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ConnectionTimeout);
        await using var pipe = new NamedPipeClientStream(
            ".",
            options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            impersonationLevel,
            HandleInheritability.None);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

        if (!serverIdentityVerifier.IsExpectedServer(pipe))
        {
            throw new LocalIpcServerIdentityException();
        }

        timeout.CancelAfter(options.ReadTimeout);
        await pipe.WriteAsync(requestBytes, timeout.Token).ConfigureAwait(false);
        await pipe.WriteAsync("\n"u8.ToArray(), timeout.Token).ConfigureAwait(false);
        await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);

        var responseBytes = await ReadLineAsync(pipe, options.MaxResponseBytes, timeout.Token)
            .ConfigureAwait(false);
        LocalIpcResponseEnvelope response;
        try
        {
            response = JsonSerializer.Deserialize<LocalIpcResponseEnvelope>(responseBytes, JsonOptions)
                ?? throw new LocalIpcProtocolException("The IPC response was empty.");
        }
        catch (JsonException exception)
        {
            throw new LocalIpcProtocolException($"The IPC response was malformed: {exception.Message}");
        }

        if (response.ProtocolVersion != LocalIpcProtocol.CurrentVersion)
        {
            throw new LocalIpcProtocolMismatchException(
                LocalIpcProtocol.CurrentVersion,
                response.ProtocolVersion);
        }

        if (!string.Equals(response.RequestId, requestId, StringComparison.Ordinal)
            || !string.Equals(response.CorrelationId, effectiveCorrelationId, StringComparison.Ordinal)
            || !IsSafeToken(response.CorrelationId))
        {
            throw new LocalIpcProtocolException("The IPC response envelope did not match the request.");
        }

        if (!response.Success)
        {
            return new(
                false,
                default,
                requestId,
                response.CorrelationId,
                response.Error?.Code ?? "ipc_error",
                response.Error?.Message ?? "The IPC operation failed.");
        }

        if (response.Result is not { } result)
        {
            throw new LocalIpcProtocolException("The successful IPC response did not contain a result.");
        }

        try
        {
            return new(
                true,
                result.Deserialize<T>(JsonOptions),
                requestId,
                response.CorrelationId,
                null,
                null);
        }
        catch (JsonException exception)
        {
            throw new LocalIpcProtocolException($"The IPC result was malformed: {exception.Message}");
        }
    }

    private static async Task<byte[]> ReadLineAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var result = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new LocalIpcProtocolException("The IPC response was incomplete.");
            }

            if (buffer[0] == (byte)'\n')
            {
                return result.ToArray();
            }

            if (buffer[0] != (byte)'\r')
            {
                if (result.Length >= maximumBytes)
                {
                    throw new LocalIpcProtocolException("The IPC response exceeded the configured size limit.");
                }

                result.WriteByte(buffer[0]);
            }
        }
    }

    private static bool IsSafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => character is >= '!' and <= '~');
}
