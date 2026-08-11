using System.Security.Claims;

namespace RmsSupportHub.Pos.Agent.Authorization;

public interface IAdministratorGroupChecker
{
    bool IsInAdministratorsGroup(ClaimsPrincipal principal);
}
