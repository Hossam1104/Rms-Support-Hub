using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

public sealed class FakeAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Test-only authentication for TestServer. The production Agent never registers this scheme; the
/// IntegrationTest host environment substitutes it because Negotiate requires a live SSPI/Kestrel
/// connection that an in-memory server cannot provide.
/// </summary>
public sealed class FakeAuthenticationHandler(
    IOptionsMonitor<FakeAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<FakeAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentIntegrationTest";
    public const string PrincipalNameHeader = "X-Test-Principal-Name";
    public const string PrincipalSidHeader = "X-Test-Principal-Sid";
    public const string IsAdministratorHeader = "X-Test-Is-Administrator";
    public const string DefaultSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(PrincipalNameHeader, out var principalName)
            || principalName.Count != 1
            || string.IsNullOrWhiteSpace(principalName[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var sid = Request.Headers.TryGetValue(PrincipalSidHeader, out var sidHeader)
            && sidHeader.Count == 1
            ? sidHeader[0]
            : DefaultSid;
        var isAdministrator = Request.Headers.TryGetValue(IsAdministratorHeader, out var adminHeader)
            && string.Equals(adminHeader.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, principalName[0]!),
            new(ClaimTypes.PrimarySid, sid!)
        };
        if (isAdministrator)
        {
            claims.Add(new Claim("test-is-administrator", "true"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
