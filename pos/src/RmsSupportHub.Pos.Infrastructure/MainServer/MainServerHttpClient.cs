using System.Net;
using System.Text.Json;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.MainServer;

/// <summary>
/// Fixed, read-only Main Server adapter. Each request is constructed from a server-owned profile
/// and discovered identity; there is no generic route, method, header, URL, or body input.
/// </summary>
public sealed class MainServerHttpClient(HttpClient httpClient) : IMainServerReadOnlyClient
{
    private const int MaxResponseBytes = 1024 * 1024;

    public async Task<MainServerReadResult> ReadStateAsync(
        MainServerProfileDefinition profile,
        RmsInstallationSnapshot installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(installation);
        if (!profile.Enabled || !profile.AllowedReadOperations.Contains(MainServerReadOperation.BranchStatus))
        {
            return new(MainServerReadOutcome.NotAttempted, null, null, "The selected Main Server profile is not enabled for this read.");
        }

        var branchCode = Uri.EscapeDataString(installation.BranchCode ?? string.Empty);
        var posNumber = Uri.EscapeDataString(installation.PosNumber ?? string.Empty);
        var requests = new[]
        {
            CreateGet(profile.BaseAddress, $"api/Branch/GetBranchesWithStatus?api-version=1&branchName={branchCode}"),
            CreateGet(profile.BaseAddress, $"api/PosMachine/GetAllPosStatuses?api-version=1&branchCode={branchCode}&posNumber={posNumber}")
        };

        try
        {
            using var branchResponse = await httpClient.SendAsync(requests[0], HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            using var posResponse = await httpClient.SendAsync(requests[1], HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var branch = await ReadSafeStateAsync(branchResponse, cancellationToken).ConfigureAwait(false);
            var pos = await ReadSafeStateAsync(posResponse, cancellationToken).ConfigureAwait(false);

            if (!branchResponse.IsSuccessStatusCode || !posResponse.IsSuccessStatusCode)
            {
                return new(MainServerReadOutcome.OutcomeUnknown, branch, pos,
                    "The Main Server read was dispatched but did not return an authoritative success response.");
            }

            return new(MainServerReadOutcome.Succeeded, branch, pos,
                "Typed read-only Branch/POS state evidence was returned by the bound Main Server profile.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(MainServerReadOutcome.OutcomeUnknown, null, null,
                "The Main Server read may have been dispatched; its outcome is unknown.");
        }
        catch (HttpRequestException)
        {
            return new(MainServerReadOutcome.OutcomeUnknown, null, null,
                "The bound Main Server could not be read; no response details are retained.");
        }
        catch
        {
            return new(MainServerReadOutcome.OutcomeUnknown, null, null,
                "The bound Main Server returned an unreadable response; no response details are retained.");
        }
    }

    private static HttpRequestMessage CreateGet(Uri baseAddress, string relativePath)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, relativePath));
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private static async Task<string?> ReadSafeStateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes) return null;
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var limited = new LimitedReadStream(stream, MaxResponseBytes);
        using var document = await JsonDocument.ParseAsync(limited, new JsonDocumentOptions { MaxDepth = 16 }, cancellationToken).ConfigureAwait(false);
        return FindState(document.RootElement);
    }

    private static string? FindState(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("status", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("state", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("isOnline", StringComparison.OrdinalIgnoreCase))
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.True => "online",
                        JsonValueKind.False => "offline",
                        JsonValueKind.Number => property.Value.GetRawText(),
                        _ => null
                    };
                    if (IsSafeStateValue(value)) return value;
                }

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    var nested = FindState(property.Value);
                    if (nested is not null) return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindState(child);
                if (nested is not null) return nested;
            }
        }

        return null;
    }

    private static bool IsSafeStateValue(string? value)
    {
        if (value is not { Length: > 0 and <= 128 }
            || value.Any(char.IsControl)
            || value.Contains('=')
            || value.Contains(':')
            || value.Contains('\\')
            || value.Contains('/')) return false;

        var lower = value.ToLowerInvariant();
        return !lower.Contains("password", StringComparison.Ordinal)
            && !lower.Contains("passwd", StringComparison.Ordinal)
            && !lower.Contains("secret", StringComparison.Ordinal)
            && !lower.Contains("token", StringComparison.Ordinal)
            && !lower.Contains("credential", StringComparison.Ordinal)
            && !lower.Contains("connection", StringComparison.Ordinal)
            && !lower.Contains("private key", StringComparison.Ordinal)
            && !lower.Contains("sid", StringComparison.Ordinal);
    }

    private sealed class LimitedReadStream(Stream inner, int limit) : Stream
    {
        private int read;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        // Network response streams are commonly non-seekable and do not expose Length.
        // Returning the configured bound keeps JSON parsing bounded without probing the
        // underlying stream for metadata.
        public override long Length => limit;
        public override long Position { get => read; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var remaining = limit - read;
            if (remaining <= 0) throw new InvalidDataException("The Main Server response exceeded the bound.");
            var count = inner.Read(buffer[..Math.Min(buffer.Length, remaining)]);
            read += count;
            return count;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var remaining = limit - read;
            if (remaining <= 0) throw new InvalidDataException("The Main Server response exceeded the bound.");
            var count = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, remaining)], cancellationToken).ConfigureAwait(false);
            read += count;
            return count;
        }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
