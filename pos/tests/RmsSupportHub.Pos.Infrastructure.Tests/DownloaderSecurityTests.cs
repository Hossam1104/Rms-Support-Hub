using System.Net;
using System.Net.Http;
using System.Text;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Http;
using RmsSupportHub.Pos.Infrastructure.Smb;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class DownloaderSecurityTests
{
    [Theory]
    [InlineData("ftp://198.51.100.10/trigger")]
    [InlineData("https://user:password@198.51.100.10/trigger")]
    [InlineData("https://127.0.0.1/trigger")]
    [InlineData("https://169.254.169.254/trigger")]
    [InlineData("https://[::ffff:127.0.0.1]/trigger")]
    [InlineData("https://2130706433/trigger")]
    public async Task UnsafeTriggerEndpointForms_FailClosedBeforeSending(string endpoint)
    {
        var handler = new CountingHandler();
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var result = await client.TriggerBackupAsync(endpoint, ["B01"]);
        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task PrivateConfiguredLiteral_IsAllowedOnlyAsTheExactServerOwnedTarget()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("10.0.0.10")));

        var result = await client.TriggerBackupAsync("https://10.0.0.10/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.Accepted, result.State);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task NonSuccessResponseAfterDispatch_IsOutcomeUnknownWithoutRemoteContract(HttpStatusCode statusCode)
    {
        var handler = new CountingHandler(statusCode);
        var client = new BackupApiClient(
            new HttpClient(handler),
            new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var result = await client.TriggerBackupAsync("https://198.51.100.10/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, result.State);
        Assert.Equal(DownloaderFailureCodes.TriggerOutcomeUnknown, result.FailureCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task HostnameResolvingToPrivateSpace_IsRejectedToPreventDnsSsrfBypass()
    {
        var handler = new CountingHandler();
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("10.0.0.10")));

        var result = await client.TriggerBackupAsync("https://backup.example/trigger", ["B01"]);
        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task UnsafeRedirect_IsRejectedAndNeverFollowed()
    {
        var handler = new CountingHandler(HttpStatusCode.Redirect, new Uri("https://127.0.0.1/metadata"));
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var result = await client.TriggerBackupAsync("https://198.51.100.10/trigger", ["B01"]);
        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, result.State);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ConnectionBoundTransport_RejectsPublicPreflightToPrivateRebindBeforeAnySocket()
    {
        var resolver = new SequenceResolver(IPAddress.Parse("198.51.100.10"), IPAddress.Loopback);
        var socket = new RecordingSocketConnector();
        var transport = new ConnectionBoundSocketConnector(resolver, socket.ConnectAsync);
        using var client = new HttpClient(BackupApiHttpMessageHandlerFactory.Create(transport));
        var api = new BackupApiClient(client, resolver);

        var result = await api.TriggerBackupAsync("http://backup.example/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Equal(0, socket.ConnectCalls);
        Assert.Equal(2, resolver.ResolveCalls);
        Assert.Equal(0, socket.TotalRequestBytes);
    }

    [Fact]
    public async Task ConnectionBoundTransport_RejectsMappedIpv4LoopbackAtConnectionTime()
    {
        var resolver = new SequenceResolver(
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("::ffff:127.0.0.1"));
        var socket = new RecordingSocketConnector();
        var transport = new ConnectionBoundSocketConnector(resolver, socket.ConnectAsync);
        using var client = new HttpClient(BackupApiHttpMessageHandlerFactory.Create(transport));
        var api = new BackupApiClient(client, resolver);

        var result = await api.TriggerBackupAsync("http://backup.example/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Equal(0, socket.ConnectCalls);
        Assert.Equal(0, socket.TotalRequestBytes);
    }

    [Fact]
    public async Task ConnectionBoundTransport_RejectsMappedIpv4PrivateAddressAtConnectionTime()
    {
        var resolver = new SequenceResolver(
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("::ffff:10.0.0.10"));
        var socket = new RecordingSocketConnector();
        var transport = new ConnectionBoundSocketConnector(resolver, socket.ConnectAsync);
        using var client = new HttpClient(BackupApiHttpMessageHandlerFactory.Create(transport));
        var api = new BackupApiClient(client, resolver);

        var result = await api.TriggerBackupAsync("http://backup.example/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Equal(0, socket.ConnectCalls);
        Assert.Equal(0, socket.TotalRequestBytes);
    }

    [Fact]
    public async Task ConnectionBoundTransport_RevalidatesARedirectBeforeOpeningItsSocket()
    {
        var resolver = new SequenceResolver(
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("10.0.0.10"));
        var socket = new RecordingSocketConnector(_ =>
            new FakeHttpStream(
                "HTTP/1.1 302 Found\r\nLocation: http://backup.example/next\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"));
        var transport = new ConnectionBoundSocketConnector(resolver, socket.ConnectAsync);
        using var client = new HttpClient(BackupApiHttpMessageHandlerFactory.Create(transport));
        var api = new BackupApiClient(client, resolver);

        var result = await api.TriggerBackupAsync("http://backup.example/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, result.State);
        Assert.Equal(1, socket.ConnectCalls);
        Assert.Equal(4, resolver.ResolveCalls);
    }

    [Fact]
    public async Task ConnectionBoundTransport_AllowsAnApprovedEndpointThroughTheProductionCallback()
    {
        var resolver = new SequenceResolver(
            IPAddress.Parse("198.51.100.10"),
            IPAddress.Parse("198.51.100.10"));
        var socket = new RecordingSocketConnector(_ =>
            new FakeHttpStream("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"));
        var transport = new ConnectionBoundSocketConnector(resolver, socket.ConnectAsync);
        using var client = new HttpClient(BackupApiHttpMessageHandlerFactory.Create(transport));
        var api = new BackupApiClient(client, resolver);

        var result = await api.TriggerBackupAsync("http://backup.example/trigger", ["B01"]);

        Assert.Equal(DownloaderTriggerState.Accepted, result.State);
        Assert.Equal(1, socket.ConnectCalls);
        Assert.True(socket.TotalRequestBytes > 0);
        Assert.Equal(2, resolver.ResolveCalls);
    }

    [Fact]
    public async Task CancellationBeforeDispatch_IsNotAttemptedAndSendsNoRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new CountingHandler();
        var client = new BackupApiClient(
            new HttpClient(handler),
            new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var result = await client.TriggerBackupAsync(
            "https://198.51.100.10/trigger",
            ["B01"],
            cancellation.Token);

        Assert.Equal(DownloaderTriggerState.NotAttempted, result.State);
        Assert.Null(result.FailureCode);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CancellationAfterDispatch_IsOutcomeUnknownWithStableCode()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new DispatchThenCancelHandler();
        var client = new BackupApiClient(
            new HttpClient(handler),
            new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var trigger = client.TriggerBackupAsync(
            "https://198.51.100.10/trigger",
            ["B01"],
            cancellation.Token);
        await handler.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var result = await trigger;

        Assert.True(handler.RequestBytesSent);
        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, result.State);
        Assert.Equal(DownloaderFailureCodes.TriggerOutcomeUnknown, result.FailureCode);
    }

    [Fact]
    public async Task TransportFailureAfterDispatch_IsOutcomeUnknownWithStableCode()
    {
        var handler = new ThrowAfterDispatchHandler();
        var client = new BackupApiClient(
            new HttpClient(handler),
            new FixedResolver(IPAddress.Parse("198.51.100.10")));

        var result = await client.TriggerBackupAsync(
            "https://198.51.100.10/trigger",
            ["B01"]);

        Assert.True(handler.RequestBytesSent);
        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, result.State);
        Assert.Equal(DownloaderFailureCodes.TriggerOutcomeUnknown, result.FailureCode);
    }

    [Fact]
    public void SmbPathPolicy_UsesCanonicalRootAndRejectsTraversalOrUnsafeServers()
    {
        var unc = SmbPathResolver.ToUncPath(
            "192.0.2.10",
            @"D:\DbBackups\batch\B01_1.zip",
            @"D:\DbBackups");

        Assert.Equal(@"\\192.0.2.10\D$\DbBackups\batch\B01_1.zip", unc);
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateCanonicalRoot(@"\\server\share"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateDrivePath(@"D:\DbBackups\..\other"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateRemoteFilePath(@"D:\Other\B01_1.zip", @"D:\DbBackups"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("127.0.0.1"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("169.254.169.254"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("rdb-server"));
    }

    [Fact]
    public void PreExistingSmbConnection_IsNeverCancelledByThisScope()
    {
        var api = new FakeSmbApi { AddResult = 85 };
        using var scope = SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api);

        Assert.Equal(SmbConnectionOutcome.CompatiblePreExisting, scope.Outcome);
        Assert.False(scope.OwnsConnection);
        Assert.Empty(api.CancelledRoots);
    }

    [Fact]
    public void MissingSmbCredentials_UsesTheServiceIdentityWithoutCreatingOwnership()
    {
        var api = new FakeSmbApi { AddResult = 1219 };
        using var scope = SmbConnectionScope.Connect(@"\\[2001:db8::10]\D$", string.Empty, string.Empty, api);

        Assert.Equal(SmbConnectionOutcome.NoCredentialRequired, scope.Outcome);
        Assert.False(scope.OwnsConnection);
        Assert.Empty(api.CancelledRoots);
    }

    [Fact]
    public void OwnedSmbConnection_IsCancelledExactlyOnceOnDispose()
    {
        var api = new FakeSmbApi { AddResult = 0 };
        var scope = SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api);
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(SmbConnectionOutcome.EstablishedByScope, scope.Outcome);
        Assert.Single(api.CancelledRoots);
        Assert.False(api.CancelledRoots[0].Force);
    }

    [Fact]
    public void CredentialConflict_IsStableAndDoesNotExposeTheShareOrCredential()
    {
        var api = new FakeSmbApi { AddResult = 1219 };

        var exception = Assert.Throws<SmbConnectionException>(() =>
            SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api));

        Assert.Equal("downloader.smb_credential_conflict", exception.Code);
        Assert.DoesNotContain("192.0.2.10", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    private sealed class CountingHandler(HttpStatusCode statusCode = HttpStatusCode.OK, Uri? location = null) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(statusCode) { RequestMessage = request };
            if (location is not null) response.Headers.Location = location;
            return Task.FromResult(response);
        }
    }

    private sealed class DispatchThenCancelHandler : HttpMessageHandler
    {
        public TaskCompletionSource DispatchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool RequestBytesSent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBytesSent = true;
            DispatchStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class ThrowAfterDispatchHandler : HttpMessageHandler
    {
        public bool RequestBytesSent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBytesSent = true;
            throw new HttpRequestException("transport details must not cross the trigger boundary");
        }
    }

    private sealed class FixedResolver(params IPAddress[] addresses) : IHostAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<IPAddress>)addresses);
    }

    private sealed class SequenceResolver(params IPAddress[] addresses) : IHostAddressResolver
    {
        private readonly Queue<IPAddress> _addresses = new(addresses);
        private IPAddress _last = addresses[^1];

        public int ResolveCalls { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            if (_addresses.Count > 0) _last = _addresses.Dequeue();
            return Task.FromResult((IReadOnlyList<IPAddress>)[_last]);
        }
    }

    private sealed class RecordingSocketConnector(Func<int, FakeHttpStream>? streamFactory = null)
    {
        private readonly Func<int, FakeHttpStream> _streamFactory = streamFactory ?? (_ =>
            new FakeHttpStream("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"));

        public int ConnectCalls { get; private set; }

        public int TotalRequestBytes { get; private set; }

        public ValueTask<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            ConnectCalls++;
            var stream = _streamFactory(ConnectCalls);
            stream.BytesWritten += count => TotalRequestBytes += count;
            return ValueTask.FromResult<Stream>(stream);
        }
    }

    private sealed class FakeHttpStream(string response) : Stream
    {
        private readonly byte[] _response = Encoding.ASCII.GetBytes(response);
        private int _offset;

        public event Action<int>? BytesWritten;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _response.Length;
        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, _response.Length - _offset);
            if (read <= 0) return 0;
            Array.Copy(_response, _offset, buffer, offset, read);
            _offset += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = Math.Min(buffer.Length, _response.Length - _offset);
            if (read <= 0) return 0;
            _response.AsSpan(_offset, read).CopyTo(buffer);
            _offset += read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Write(byte[] buffer, int offset, int count) => BytesWritten?.Invoke(count);

        public override void Write(ReadOnlySpan<byte> buffer) => BytesWritten?.Invoke(buffer.Length);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.Run(() => BytesWritten?.Invoke(count), cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BytesWritten?.Invoke(buffer.Length);
            return ValueTask.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class FakeSmbApi : ISmbConnectionApi
    {
        public int AddResult { get; init; }

        public List<(string Root, bool Force)> CancelledRoots { get; } = [];

        public int Add(string shareRoot, string username, string password) => AddResult;

        public int Cancel(string shareRoot, bool force)
        {
            CancelledRoots.Add((shareRoot, force));
            return 0;
        }
    }
}
