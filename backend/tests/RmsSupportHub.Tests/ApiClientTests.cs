using System.Net;
using System.Net.Http;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task SendOrderWithApiKeyUsesOnlyTheFixedApiKeyHeader()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new ApiClient(httpClient);

        var result = await client.SendOrderWithApiKeyAsync(
            "https://uni.testing.example/create",
            new { order = "synthetic" },
            "TEST-ONLY-API-KEY");

        Assert.True(result.Success);
        Assert.Equal("TEST-ONLY-API-KEY", handler.Request!.Headers.GetValues("X-Api-Key").Single());
        Assert.DoesNotContain("Authorization", handler.Request.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task SendOrderWithoutApiKeyDoesNotAddAuthenticationHeader()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new ApiClient(httpClient);

        var result = await client.SendOrderAsync("https://testing.example/create", new { order = "synthetic" });

        Assert.True(result.Success);
        Assert.False(handler.Request!.Headers.Contains("X-Api-Key"));
        Assert.False(handler.Request.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task SendOrderWithMissingApiKeyFailsBeforeOutboundCall()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new ApiClient(httpClient);

        var result = await client.SendOrderWithApiKeyAsync(
            "https://uni.testing.example/create",
            new { order = "synthetic" },
            " ");

        Assert.False(result.Success);
        Assert.Null(handler.Request);
        Assert.DoesNotContain("X-Api-Key", result.ResponseText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        }
    }
}
