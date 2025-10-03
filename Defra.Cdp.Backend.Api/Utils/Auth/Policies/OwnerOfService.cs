using Defra.Cdp.Backend.Api.Services.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth.Policies;

public class OwnerOfEntityAuthorizationHandler(IEntitiesService entitiesService) : AuthorizationHandler<OwnerOfEntity, object>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OwnerOfEntity requirement,
        object resource)
    {
        if (context.Resource is HttpContext httpContext)
        {
            var entityName = httpContext.GetRouteValue(requirement.Path);
            if (entityName == null) return;
            
            var entity = await entitiesService.GetEntity(entityName.ToString()!, httpContext.RequestAborted);
            if (entity == null) return;
            
            var validPermissions = entity.Teams.Select(t => $"permission:serviceOwner:team:{t.TeamId}");
            if (context.User.HasClaim(c => c.Type == "cdp-claim" && validPermissions.Contains(c.Value)))
            {
                context.Succeed(requirement);
            }

            if (context.User.HasClaim(c => c is { Type: "cdp-claim", Value: "permission:admin" }))
            {
                context.Succeed(requirement);
            }
        }
    }
}

public class OwnerOfEntity(string path) : IAuthorizationRequirement
{
    public string Path { get; init; } = path;
};