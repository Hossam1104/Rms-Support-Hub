using Microsoft.AspNetCore.Authorization;

namespace RmsSupportHub.Pos.Agent.Authorization;

public sealed class LocalAdministratorsOnlyHandler(IAdministratorGroupChecker checker)
    : AuthorizationHandler<LocalAdministratorsOnlyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LocalAdministratorsOnlyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && checker.IsInAdministratorsGroup(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
