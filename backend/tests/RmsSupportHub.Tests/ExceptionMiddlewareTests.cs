using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Api.Middleware;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>Proves the R6 fix for remediation_plan.md B22: every exception
/// maps to the uniform `{ error: { code, message, details } }` envelope with
/// the correct status code -- previously every exception, including
/// database failures, was flattened to a 400 with a `{success,message,error}`
/// shape.</summary>
public class ExceptionMiddlewareTests
{
    private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(Exception thrown)
    {
        var middleware = new ExceptionMiddleware(_ => throw thrown, NullLogger<ExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, JsonDocument.Parse(json).RootElement.Clone());
    }

    public static IEnumerable<object[]> ApiExceptions()
    {
        yield return new object[] { new NotFoundException("boom"), 404, "not_found" };
        yield return new object[] { new BadRequestException("boom"), 400, "bad_request" };
        yield return new object[] { new ConflictException("boom"), 409, "conflict" };
        yield return new object[] { new UpstreamException("boom"), 502, "upstream_error" };
        yield return new object[] { new FeatureNotSupportedException("boom"), 501, "feature_not_supported" };
        yield return new object[] { new ConfigurationException("boom"), 500, "configuration_error" };
    }

    [Theory]
    [MemberData(nameof(ApiExceptions))]
    public async Task ApiException_MapsToItsOwnStatusCodeAndCode(ApiException exception, int expectedStatus, string expectedCode)
    {
        var (statusCode, body) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, statusCode);
        Assert.Equal(expectedCode, body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("boom", body.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnknownException_MapsTo500_NotTheOldFlat400()
    {
        var (statusCode, body) = await InvokeAsync(new InvalidOperationException("unexpected failure"));

        Assert.Equal(500, statusCode);
        Assert.Equal("internal_error", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("unexpected failure", body.GetProperty("error").GetProperty("message").GetString());
    }
}
