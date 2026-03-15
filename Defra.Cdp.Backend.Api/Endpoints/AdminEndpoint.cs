using Defra.Cdp.Backend.Api.Services.Entities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    public static void MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/entity/status", UpdateStatus);
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

}
