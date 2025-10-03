using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth.Policies;

public class MemberOfTeamAuthorizationHandler : AuthorizationHandler<MemberOfTeam, string>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        MemberOfTeam requirement,
        string teamId)
    {
        if (context.User.HasClaim(c => c.Value == $"team:{teamId}"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class MemberOfTeam : IAuthorizationRequirement;