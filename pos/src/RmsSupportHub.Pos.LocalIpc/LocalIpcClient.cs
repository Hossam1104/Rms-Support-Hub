using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;

namespace RmsSupportHub.Pos.LocalIpc;

public sealed record LocalIpcCallResult<T>(
    bool Succeeded,
    T? Result,
    string RequestId,
    string CorrelationId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed class LocalIpcProtocolException(string message) : Exception(message);

/// <summary>
/// Small typed client for the WPF-to-Agent local IPC contract. It exposes only the operations
/// implemented by WPF-01; arbitrary operation names cannot be supplied by callers.
/// </summary>
public sealed class LocalIpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly LocalIpcOptions options;

    public LocalIpcClient(LocalIpcOptions? options = null)
    {
        this.options = options ?? new LocalIpcOptions();
        this.options.Validate();
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

    private async Task<LocalIpcCallResult<T>> SendAsync<T>(
        string operation,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var effectiveCorrelationId = IsSafeToken(correlationId) ? correlationId! : requestId;
        var request = new LocalIpcRequestEnvelope(
            LocalIpcProtocol.CurrentVersion,
            requestId,
            effectiveCorrelationId,
            operation,
            null);
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
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

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

        if (response.ProtocolVersion != LocalIpcProtocol.CurrentVersion
            || !string.Equals(response.RequestId, requestId, StringComparison.Ordinal)
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
        while (result.Length <= maximumBytes)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == (byte)'\n')
            {
                return result.ToArray();
            }

            if (buffer[0] != (byte)'\r')
            {
                result.WriteByte(buffer[0]);
            }
        }

        throw new LocalIpcProtocolException("The IPC response exceeded the configured size limit.");
    }

    private static bool IsSafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => character is >= '!' and <= '~');
}
