using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Agent.LocalIpc;

/// <summary>
/// Bounded Windows Named Pipe server. The server owns transport framing and identity extraction;
/// typed business execution is delegated to the shared Application handler.
/// </summary>
public sealed class LocalIpcServer(
    LocalIpcOptions options,
    ILocalIpcOperatorGroupResolver operatorGroupResolver,
    ILocalIpcSecurityDescriptorFactory securityDescriptorFactory,
    IAgentInvocationContextFactory contextFactory,
    RmsInstallationDiscoveryQueryHandler installationDiscovery,
    LocalIpcRuntimeStatus status,
    ILogger<LocalIpcServer> logger) : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly ConcurrentDictionary<Task, byte> activeClients = new();
    private CancellationTokenSource? lifetime;
    private Task? acceptTask;
    private SecurityIdentifier? operatorGroupSid;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        if (!options.Enabled)
        {
            status.SetDisabled();
            return Task.CompletedTask;
        }

        if (!OperatingSystem.IsWindows())
        {
            status.SetUnavailable("windows_required");
            return Task.CompletedTask;
        }

        if (!operatorGroupResolver.TryResolve(options.OperatorGroupName, out operatorGroupSid))
        {
            status.SetUnavailable("operator_group_unavailable");
            logger.LogError(
                "Local IPC is enabled but the configured operator group could not be resolved. The IPC feature remains disabled.");
            return Task.CompletedTask;
        }

        status.SetStarting();
        lifetime = new CancellationTokenSource();
        acceptTask = AcceptLoopAsync(lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lifetime?.Cancel();
        if (acceptTask is not null)
        {
            await AwaitWithoutThrowingAsync(acceptTask, cancellationToken).ConfigureAwait(false);
        }

        var clients = activeClients.Keys.ToArray();
        if (clients.Length > 0)
        {
            await AwaitWithoutThrowingAsync(Task.WhenAll(clients), cancellationToken).ConfigureAwait(false);
        }

        lifetime?.Dispose();
        lifetime = null;
        acceptTask = null;
        status.SetDisabled();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        using var concurrency = new SemaphoreSlim(options.MaxConcurrentClients);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = NamedPipeServerStreamAcl.Create(
                        options.PipeName,
                        PipeDirection.InOut,
                        options.MaxConcurrentClients,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        securityDescriptorFactory.Create(operatorGroupSid!));
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    status.SetListening();

                    var clientTask = HandleClientAsync(pipe, concurrency, cancellationToken);
                    pipe = null;
                    activeClients.TryAdd(clientTask, 0);
                    _ = ObserveClientAsync(clientTask);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    concurrency.Release();
                    pipe?.Dispose();
                    break;
                }
                catch (Exception exception)
                {
                    concurrency.Release();
                    pipe?.Dispose();
                    status.SetUnavailable("listener_initialization_failed");
                    logger.LogError(exception, "The local IPC listener failed closed.");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task ObserveClientAsync(Task clientTask)
    {
        try
        {
            await clientTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "A local IPC client connection ended with a transport error.");
        }
        finally
        {
            activeClients.TryRemove(clientTask, out _);
        }
    }

    private async Task HandleClientAsync(
        NamedPipeServerStream pipe,
        SemaphoreSlim concurrency,
        CancellationToken serverCancellationToken)
    {
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken);
                timeout.CancelAfter(options.ReadTimeout);
                var requestBytes = await ReadLineAsync(pipe, options.MaxRequestBytes, timeout.Token)
                    .ConfigureAwait(false);
                if (requestBytes is null)
                {
                    return;
                }

                if (!TryDeserializeRequest(requestBytes, out var request))
                {
                    await WriteErrorAsync(
                        pipe,
                        "unavailable",
                        NewCorrelationId(),
                        "malformed_request",
                        "The IPC request was malformed.",
                        timeout.Token).ConfigureAwait(false);
                    return;
                }

                var requestId = request.RequestId;
                var correlationId = request.CorrelationId ?? requestId;
                if (request.ProtocolVersion != LocalIpcProtocol.CurrentVersion)
                {
                    await WriteErrorAsync(
                        pipe,
                        requestId,
                        correlationId,
                        "unsupported_protocol_version",
                        "The IPC protocol version is not supported.",
                        timeout.Token).ConfigureAwait(false);
                    return;
                }

                if (!IsSafeToken(requestId) || !IsSafeToken(correlationId))
                {
                    await WriteErrorAsync(
                        pipe,
                        IsSafeToken(requestId) ? requestId : "unavailable",
                        IsSafeToken(correlationId) ? correlationId : NewCorrelationId(),
                        "invalid_request_identity",
                        "The IPC request identity was invalid.",
                        timeout.Token).ConfigureAwait(false);
                    return;
                }

                using var identity = GetClientIdentity(pipe);
                if (identity is null || operatorGroupSid is null)
                {
                    await WriteErrorAsync(
                        pipe,
                        requestId,
                        correlationId,
                        "caller_identity_unavailable",
                        "The authenticated Windows caller could not be resolved.",
                        timeout.Token).ConfigureAwait(false);
                    return;
                }

                var context = contextFactory.CreateLocalWpf(identity, operatorGroupSid, correlationId);
                await DispatchAsync(pipe, request, context, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (serverCancellationToken.IsCancellationRequested)
            {
                // Normal server shutdown.
            }
            catch (OperationCanceledException)
            {
                // Per-connection timeout. Closing the pipe is the bounded response.
            }
            catch (LocalIpcProtocolException exception)
            {
                try
                {
                    await WriteErrorAsync(
                        pipe,
                        "unavailable",
                        NewCorrelationId(),
                        exception.Message.Contains("size limit", StringComparison.Ordinal)
                            ? "request_too_large"
                            : "invalid_request",
                        "The IPC request was rejected.",
                        serverCancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // The connection is closed if the bounded error cannot be returned.
                }
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "A local IPC request failed closed.");
            }
            finally
            {
                concurrency.Release();
            }
        }
    }

    private async Task DispatchAsync(
        NamedPipeServerStream pipe,
        LocalIpcRequestEnvelope request,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        switch (request.Operation)
        {
            case LocalIpcProtocol.HealthOperation:
                await DispatchHealthAsync(pipe, request, context, cancellationToken).ConfigureAwait(false);
                return;

            case LocalIpcProtocol.InstallationDiscoveryOperation:
                var result = await installationDiscovery
                    .HandleAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                if (!result.Succeeded || result.Value is null)
                {
                    await WriteErrorAsync(
                        pipe,
                        request.RequestId,
                        request.CorrelationId!,
                        result.Error?.Code ?? "diagnostic_unavailable",
                        result.Error?.Message ?? "The diagnostic query failed.",
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteSuccessAsync(
                    pipe,
                    request.RequestId,
                    request.CorrelationId!,
                    RmsInstallationContractMapper.Map(result.Value),
                    cancellationToken).ConfigureAwait(false);
                return;

            default:
                await WriteErrorAsync(
                    pipe,
                    request.RequestId,
                    request.CorrelationId!,
                    "unknown_operation",
                    "The requested IPC operation is not supported.",
                    cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async Task DispatchHealthAsync(
        NamedPipeServerStream pipe,
        LocalIpcRequestEnvelope request,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);
        if (!decision.Allowed)
        {
            await WriteErrorAsync(
                pipe,
                request.RequestId,
                request.CorrelationId!,
                decision.Code,
                decision.Message,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteSuccessAsync(
            pipe,
            request.RequestId,
            request.CorrelationId!,
            status.GetHealth(),
            cancellationToken).ConfigureAwait(false);
    }

    private static WindowsIdentity? GetClientIdentity(NamedPipeServerStream pipe)
    {
        WindowsIdentity? identity = null;
        try
        {
            pipe.RunAsClient(() => identity = WindowsIdentity.GetCurrent());
            return identity;
        }
        catch
        {
            identity?.Dispose();
            return null;
        }
    }

    private async Task WriteSuccessAsync(
        Stream pipe,
        string requestId,
        string correlationId,
        object result,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions));
        var response = new LocalIpcResponseEnvelope(
            LocalIpcProtocol.CurrentVersion,
            requestId,
            correlationId,
            true,
            document.RootElement.Clone(),
            null);
        await WriteResponseAsync(pipe, response, cancellationToken).ConfigureAwait(false);
    }

    private Task WriteErrorAsync(
        Stream pipe,
        string requestId,
        string correlationId,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        WriteResponseAsync(
            pipe,
            new LocalIpcResponseEnvelope(
                LocalIpcProtocol.CurrentVersion,
                requestId,
                correlationId,
                false,
                null,
                new LocalIpcErrorDto(code, message)),
            cancellationToken);

    private async Task WriteResponseAsync(
        Stream pipe,
        LocalIpcResponseEnvelope response,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        if (bytes.Length > options.MaxResponseBytes)
        {
            throw new LocalIpcProtocolException("The IPC response exceeds the configured size limit.");
        }

        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await pipe.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool TryDeserializeRequest(byte[] bytes, out LocalIpcRequestEnvelope request)
    {
        try
        {
            request = JsonSerializer.Deserialize<LocalIpcRequestEnvelope>(bytes, JsonOptions)!;
            return request is not null;
        }
        catch (JsonException)
        {
            request = null!;
            return false;
        }
    }

    private static async Task<byte[]?> ReadLineAsync(
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
                return result.Length == 0 ? null : throw new LocalIpcProtocolException("The IPC request was incomplete.");
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

        throw new LocalIpcProtocolException("The IPC request exceeded the configured size limit.");
    }

    private static bool IsSafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => character is >= '!' and <= '~');

    private static string NewCorrelationId() => Guid.NewGuid().ToString("N");

    private static async Task AwaitWithoutThrowingAsync(Task task, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }
}
