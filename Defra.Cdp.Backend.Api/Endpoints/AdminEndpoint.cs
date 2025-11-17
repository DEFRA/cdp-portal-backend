using Defra.Cdp.Backend.Api.Services.Entities;

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
    private static async Task<IResult> UpdateStatus(IEntitiesService entitiesService)
    {
        await entitiesService.BulkUpdateEntityStatus(CancellationToken.None);
        return Results.Ok();
    }

}