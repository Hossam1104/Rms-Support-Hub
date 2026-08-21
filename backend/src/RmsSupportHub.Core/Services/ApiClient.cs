using System.Text;
using System.Text.Json;

namespace RmsSupportHub.Core.Services;

public interface IApiClient
{
    Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson);
    /// <summary>Fixed Uni-Commerce authentication seam. This is deliberately
    /// not a generic caller-controlled header dictionary.</summary>
    Task<ApiResponseResult> SendOrderWithApiKeyAsync(string url, object payloadJson, string apiKey);
    Task<bool> TestEndpointAsync(string url, TimeSpan? timeout = null);
}

public record ApiResponseResult(
    int StatusCode,
    string ResponseText,
    string UrlSent,
    bool Success
);

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponseResult> SendOrderAsync(string url, object payload)
        => await SendOrderCoreAsync(url, payload, apiKey: null);

    public async Task<ApiResponseResult> SendOrderWithApiKeyAsync(string url, object payload, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)
            || apiKey.IndexOf('\r') >= 0
            || apiKey.IndexOf('\n') >= 0)
        {
            return new ApiResponseResult(
                StatusCode: 500,
                ResponseText: "The downstream operation could not be completed.",
                UrlSent: url,
                Success: false);
        }

        return await SendOrderCoreAsync(url, payload, apiKey);
    }

    private async Task<ApiResponseResult> SendOrderCoreAsync(string url, object payload, string? apiKey)
    {
        var json = payload is string s ? s : JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Add("X-Api-Key", apiKey);

            var response = await _httpClient.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            return new ApiResponseResult(
                StatusCode: (int)response.StatusCode,
                ResponseText: responseText,
                UrlSent: url,
                Success: response.IsSuccessStatusCode
            );
        }
        catch (Exception)
        {
            return new ApiResponseResult(
                StatusCode: 500,
                ResponseText: "The downstream operation could not be completed.",
                UrlSent: url,
                Success: false
            );
        }
    }

    public async Task<bool> TestEndpointAsync(string url, TimeSpan? timeout = null)
    {
        try
        {
            var uri = new Uri(url);
            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80));
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(3));

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            return completedTask == connectTask && tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}
