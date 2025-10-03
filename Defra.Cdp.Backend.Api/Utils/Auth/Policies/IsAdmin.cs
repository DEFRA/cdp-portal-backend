using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth.Policies;

public class IsAdminAuthorizationHandler : AuthorizationHandler<IsAdmin, object>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        IsAdmin requirement,
        object resource)
    {
        if (context.User.HasClaim(c => c.Value == "permission:admin"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class IsAdmin : IAuthorizationRequirement;