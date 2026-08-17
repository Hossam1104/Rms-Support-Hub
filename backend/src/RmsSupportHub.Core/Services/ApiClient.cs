using System.Text;
using System.Text.Json;

namespace RmsSupportHub.Core.Services;

public interface IApiClient
{
    Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson);
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
