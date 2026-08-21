using Microsoft.AspNetCore.Http;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Tests;

public sealed class SessionIdMiddlewareTests
{
    [Fact]
    public async Task ProductionSessionCookieIsSecureEvenOnAnHttpRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var middleware = new SessionIdMiddleware(_ => Task.CompletedTask, new EnvironmentPolicy(DeploymentTier.Production));

        await middleware.InvokeAsync(context);

        var setCookie = context.Response.Headers.SetCookie.Single();
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.True(context.Items.ContainsKey(SessionIdMiddleware.CookieName));
    }

    [Fact]
    public async Task TestingCookieKeepsExistingSchemeDependentBehavior()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        await new SessionIdMiddleware(_ => Task.CompletedTask, new EnvironmentPolicy(DeploymentTier.Testing))
            .InvokeAsync(httpContext);

        Assert.DoesNotContain("Secure", httpContext.Response.Headers.SetCookie.Single(), StringComparison.OrdinalIgnoreCase);

        var httpsContext = new DefaultHttpContext();
        httpsContext.Request.Scheme = "https";
        await new SessionIdMiddleware(_ => Task.CompletedTask, new EnvironmentPolicy(DeploymentTier.Testing))
            .InvokeAsync(httpsContext);

        Assert.Contains("Secure", httpsContext.Response.Headers.SetCookie.Single(), StringComparison.OrdinalIgnoreCase);
    }
}
