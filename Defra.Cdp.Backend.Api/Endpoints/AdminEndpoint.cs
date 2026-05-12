using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Sboms;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    public static void MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/entity/status", UpdateStatus).RequireAuthorization(AuthPolicies.IsAdmin);
        app.MapGet("/admin/sbom/push-teams", PushSbomTeams);
        app.MapGet("/admin/auth-test/is-admin", AuthTest).RequireAuthorization(AuthPolicies.IsAdmin);
        app.MapGet("/admin/auth-test/is-tenant", AuthTest).RequireAuthorization(AuthPolicies.IsTenant);
        app.MapGet("/admin/auth-test/is-owner/{entity}", AuthOwnerTest).RequireOwnership("entity");
    }

    /// <summary>
    /// Debugging endpoint: Forces an update of the entity creation status.
    /// </summary>
    /// <param name="entitiesService"></param>
    /// <returns></returns>
    private static async Task<Ok> UpdateStatus(IEntitiesService entitiesService)
    {
        await entitiesService.BulkUpdateEntityStatus(CancellationToken.None);
        return TypedResults.Ok();
    }

    /// <summary>
    /// Syncs Entity ownership info to SBOM explorer 
    /// </summary>
    /// <param name="serviceOwnershipHandler"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private static async Task<Ok> PushSbomTeams(ISbomServiceOwnershipHandler serviceOwnershipHandler, CancellationToken ct)
    {
        await serviceOwnershipHandler.Handle(ct);
        return TypedResults.Ok();
    }
    
    private static Ok<List<string>> AuthTest(HttpRequest req, CancellationToken ct)
    {
        var claims = req.HttpContext.User.Claims.Select(c => $"{c.Type}:{c.Value}").ToList();
        return TypedResults.Ok(claims);
    }

    private static Ok<string> AuthOwnerTest(string entity, HttpRequest req, CancellationToken ct)
    {
        return TypedResults.Ok($"Owner of {entity}");
    }
}
