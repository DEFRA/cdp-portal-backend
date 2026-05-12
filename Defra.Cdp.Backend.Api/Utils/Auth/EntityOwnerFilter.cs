using System.Security.Claims;
using Defra.Cdp.Backend.Api.Services.Entities;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

/// <summary>
/// Request filter to validate the logged in user is an owner of the given entity.
/// Takes a function that extracts the name of the entity from the request (typically from route params)
/// </summary>
/// <param name="entityNameExtractor"></param>
public class EntityOwnerFilter(Func<HttpRequest, string?> entityNameExtractor) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        var entityName = entityNameExtractor(context.HttpContext.Request);
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(entityName) || userId == null)
        {
            return Results.Forbid();
        }

        var entitiesService = context.HttpContext.RequestServices.GetRequiredService<IEntitiesService>();
        var entity = await entitiesService.GetEntity(entityName, CancellationToken.None);

        if (entity == null)
        {
            return Results.Forbid();
        }

        var isAdmin = user.HasClaim("cdp", CdpScopes.Admin);
        var hasAccess = entity.Teams.Exists(team =>
            user.HasClaim("cdp", $"{CdpScopes.ServiceOwner}:team:{team.TeamId}"));

        if (isAdmin || hasAccess)
        {
            return await next(context);
        }

        return Results.Forbid();
    }
}

/// <summary>
/// C# Extension method to simplify setting up endpoints that require the user to be an owner of the enity.
/// </summary>
public static class EntityOwnerExtensions
{
    public static RouteHandlerBuilder RequireOwnership(this RouteHandlerBuilder builder, string routeParamName)
    {
        return builder.AddEndpointFilter(new EntityOwnerFilter(r => r.RouteValues[routeParamName]?.ToString()));
    }

    public static RouteHandlerBuilder RequireOwnership(this RouteHandlerBuilder builder,
        Func<HttpRequest, string?> entityNameExtractor)
    {
        return builder.AddEndpointFilter(new EntityOwnerFilter(entityNameExtractor));
    }
}