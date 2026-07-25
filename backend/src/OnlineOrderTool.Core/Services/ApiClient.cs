using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OnlineOrderTool.Core.Services;

public interface IApiClient
{
    Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson);
    Task<bool> TestEndpointAsync(string url);
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

    public static HttpClient CreateHttpClientWithSslBypass()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<ApiResponseResult> SendOrderAsync(string url, object payload)
    {
        var json = payload is string s ? s : JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            return new ApiResponseResult(
                StatusCode: (int)response.StatusCode,
                ResponseText: responseText,
                UrlSent: url,
                Success: response.IsSuccessStatusCode
            );
        }
        catch (Exception ex)
        {
            return new ApiResponseResult(
                StatusCode: 500,
                ResponseText: $"API request failed: {ex.Message}",
                UrlSent: url,
                Success: false
            );
        }
    }

    public async Task<bool> TestEndpointAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80));
            var timeoutTask = Task.Delay(3000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            return completedTask == connectTask && tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}
