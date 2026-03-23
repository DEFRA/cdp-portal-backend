using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Sboms;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    public static void MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/entity/status", UpdateStatus);
        app.MapGet("/admin/sbom/push-teams", PushSbomTeams);
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

}
