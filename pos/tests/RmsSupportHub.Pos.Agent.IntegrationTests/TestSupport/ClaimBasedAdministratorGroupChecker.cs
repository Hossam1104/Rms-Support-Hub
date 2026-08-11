using System.Security.Claims;
using RmsSupportHub.Pos.Agent.Authorization;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

public sealed class ClaimBasedAdministratorGroupChecker : IAdministratorGroupChecker
{
    public bool IsInAdministratorsGroup(ClaimsPrincipal principal) =>
        principal.HasClaim("test-is-administrator", "true");
}
