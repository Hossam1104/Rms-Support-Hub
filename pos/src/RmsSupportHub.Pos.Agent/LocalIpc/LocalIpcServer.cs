using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
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
    ServiceHealthQueryHandler serviceHealth,
    LocalIpcRuntimeStatus status,
    ILogger<LocalIpcServer> logger,
    ILocalIpcServerPipeFactory? serverPipeFactory = null) : IHostedService
{
    private static readonly TimeSpan InitialRetryBackoff = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaximumRetryBackoff = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly ConcurrentDictionary<Task, byte> activeClients = new();
    private readonly ILocalIpcServerPipeFactory pipeFactory =
        serverPipeFactory ?? new WindowsLocalIpcServerPipeFactory();
    private readonly object pipeOwnershipGate = new();
    private CancellationTokenSource? lifetime;
    private Task? acceptTask;
    private SemaphoreSlim? concurrency;
    private PipeSecurity? pipeSecurity;
    private SecurityIdentifier? operatorGroupSid;
    private int ownedPipeInstances;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            options.Validate();
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            status.SetUnavailable("invalid_options");
            logger.LogError(exception, "Local IPC options are invalid; the listener will not start.");
            return Task.CompletedTask;
        }

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

        try
        {
            if (!operatorGroupResolver.TryResolve(options.OperatorGroupName, out operatorGroupSid))
            {
                status.SetUnavailable("operator_group_unavailable");
                logger.LogError(
                    "Local IPC is enabled but the configured operator group could not be resolved. The IPC feature remains disabled.");
                return Task.CompletedTask;
            }
        }
        catch (Exception exception)
        {
            status.SetUnavailable("operator_group_unavailable");
            logger.LogError(exception, "The configured local operator group could not be resolved; the listener will not start.");
            return Task.CompletedTask;
        }

        try
        {
            pipeSecurity = securityDescriptorFactory.Create(operatorGroupSid);
        }
        catch (Exception exception)
        {
            status.SetUnavailable("security_descriptor_unavailable");
            logger.LogError(exception, "The local IPC security descriptor could not be constructed; the listener will not start.");
            return Task.CompletedTask;
        }

        status.SetStarting();
        lifetime = new CancellationTokenSource();
        concurrency = new SemaphoreSlim(options.MaxConcurrentClients);
        acceptTask = AcceptLoopAsync(lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var currentLifetime = lifetime;
        var currentAcceptTask = acceptTask;
        var currentConcurrency = concurrency;
        if (currentLifetime is null || currentConcurrency is null)
        {
            status.SetDisabled();
            return;
        }

        currentLifetime.Cancel();
        if (currentAcceptTask is not null)
        {
            await AwaitWithoutThrowingAsync(currentAcceptTask, CancellationToken.None).ConfigureAwait(false);
        }

        var clientsTask = Task.WhenAll(activeClients.Keys.ToArray());
        try
        {
            await clientsTask.WaitAsync(options.ReadTimeout + TimeSpan.FromSeconds(1), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Active handlers are still allowed to release the server-owned semaphore. The
            // continuation below owns its disposal if the bounded shutdown wait expires.
        }

        status.SetDisabled();
        if (clientsTask.IsCompleted)
        {
            CompleteShutdown(currentLifetime, currentConcurrency);
        }
        else
        {
            _ = CompleteShutdownWhenClientsFinishAsync(clientsTask, currentLifetime, currentConcurrency);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var serverConcurrency = concurrency
            ?? throw new InvalidOperationException("The local IPC semaphore was not initialized.");
        var backoff = InitialRetryBackoff;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var permitAcquired = false;
                OwnedPipeInstance? pipe = null;
                try
                {
                    await serverConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                    permitAcquired = true;
                    pipe = CreatePipe();
                    await pipe.Stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    backoff = InitialRetryBackoff;
                    status.SetListening();

                    var clientTask = HandleClientAsync(pipe, serverConcurrency, cancellationToken);
                    pipe = null;
                    permitAcquired = false;
                    activeClients.TryAdd(clientTask, 0);
                    _ = ObserveClientAsync(clientTask);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (pipe is not null)
                    {
                        await pipe.DisposeAsync().ConfigureAwait(false);
                    }

                    if (permitAcquired)
                    {
                        serverConcurrency.Release();
                    }

                    break;
                }
                catch (Exception exception)
                {
                    if (pipe is not null)
                    {
                        await pipe.DisposeAsync().ConfigureAwait(false);
                    }

                    if (permitAcquired)
                    {
                        serverConcurrency.Release();
                    }

                    status.SetUnavailable("listener_degraded");
                    logger.LogWarning(
                        exception,
                        "The local IPC listener encountered a transient transport failure and will retry in {Backoff}.",
                        backoff);
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                    backoff = NextBackoff(backoff);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown, including cancellation during bounded recovery backoff.
        }
    }

    private OwnedPipeInstance CreatePipe()
    {
        lock (pipeOwnershipGate)
        {
            var firstPipeInstance = ownedPipeInstances == 0;
            var pipeOptions = PipeOptions.Asynchronous
                | (firstPipeInstance ? PipeOptions.FirstPipeInstance : 0);
            var stream = pipeFactory.Create(
                options.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                pipeOptions,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity ?? throw new InvalidOperationException("The local IPC security descriptor was not initialized."));
            ownedPipeInstances++;
            return new OwnedPipeInstance(this, stream);
        }
    }

    private void ReleasePipeInstance()
    {
        lock (pipeOwnershipGate)
        {
            if (ownedPipeInstances > 0)
            {
                ownedPipeInstances--;
            }
        }
    }

    private static TimeSpan NextBackoff(TimeSpan current) =>
        TimeSpan.FromMilliseconds(Math.Min(MaximumRetryBackoff.TotalMilliseconds, current.TotalMilliseconds * 2));

    private async Task HandleClientAsync(
        OwnedPipeInstance pipeInstance,
        SemaphoreSlim serverConcurrency,
        CancellationToken serverCancellationToken)
    {
        try
        {
            await using (pipeInstance.ConfigureAwait(false))
            {
                var pipe = pipeInstance.Stream;
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
                await DispatchAsync(pipe, request, context, correlationId, timeout.Token).ConfigureAwait(false);
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
            }
        }
        finally
        {
            // OwnedPipeInstance.DisposeAsync has completed before the application slot is returned.
            serverConcurrency.Release();
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

    private async Task DispatchAsync(
        NamedPipeServerStream pipe,
        LocalIpcRequestEnvelope request,
        InvocationContext context,
        string effectiveCorrelationId,
        CancellationToken cancellationToken)
    {
        switch (request.Operation)
        {
            case LocalIpcProtocol.HealthOperation:
                await DispatchHealthAsync(pipe, request, context, effectiveCorrelationId, cancellationToken).ConfigureAwait(false);
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
                        effectiveCorrelationId,
                        result.Error?.Code ?? "diagnostic_unavailable",
                        result.Error?.Message ?? "The diagnostic query failed.",
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteSuccessAsync(
                    pipe,
                    request.RequestId,
                    effectiveCorrelationId,
                    RmsInstallationContractMapper.Map(result.Value),
                    cancellationToken).ConfigureAwait(false);
                return;

            case LocalIpcProtocol.ServiceHealthOperation:
                var serviceHealthResult = await serviceHealth
                    .HandleAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                if (!serviceHealthResult.Succeeded || serviceHealthResult.Value is null)
                {
                    await WriteErrorAsync(
                        pipe,
                        request.RequestId,
                        effectiveCorrelationId,
                        serviceHealthResult.Error?.Code ?? "service_health_unavailable",
                        serviceHealthResult.Error?.Message ?? "The RMS service health query could not be completed.",
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteSuccessAsync(
                    pipe,
                    request.RequestId,
                    effectiveCorrelationId,
                    ServiceHealthContractMapper.Map(serviceHealthResult.Value),
                    cancellationToken).ConfigureAwait(false);
                return;

            default:
                await WriteErrorAsync(
                    pipe,
                    request.RequestId,
                    effectiveCorrelationId,
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
        string effectiveCorrelationId,
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
                effectiveCorrelationId,
                decision.Code,
                decision.Message,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteSuccessAsync(
            pipe,
            request.RequestId,
            effectiveCorrelationId,
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
        while (true)
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
                if (result.Length >= maximumBytes)
                {
                    throw new LocalIpcProtocolException("The IPC request exceeded the configured size limit.");
                }

                result.WriteByte(buffer[0]);
            }
        }
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

    private void CompleteShutdown(
        CancellationTokenSource currentLifetime,
        SemaphoreSlim currentConcurrency)
    {
        currentConcurrency.Dispose();
        currentLifetime.Dispose();
        if (ReferenceEquals(lifetime, currentLifetime))
        {
            lifetime = null;
            acceptTask = null;
            concurrency = null;
            pipeSecurity = null;
            operatorGroupSid = null;
        }
    }

    private async Task CompleteShutdownWhenClientsFinishAsync(
        Task clientsTask,
        CancellationTokenSource currentLifetime,
        SemaphoreSlim currentConcurrency)
    {
        try
        {
            await clientsTask.ConfigureAwait(false);
        }
        catch
        {
            // Client failures are already logged by ObserveClientAsync.
        }
        finally
        {
            CompleteShutdown(currentLifetime, currentConcurrency);
        }
    }

    private sealed class OwnedPipeInstance(LocalIpcServer owner, NamedPipeServerStream stream) : IAsyncDisposable
    {
        private int disposed;

        public NamedPipeServerStream Stream { get; } = stream;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await Stream.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                owner.ReleasePipeInstance();
            }
        }
    }
}
