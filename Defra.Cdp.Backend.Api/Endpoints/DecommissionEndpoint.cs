using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DecommissionEndpoint
{
    public static void MapDecommissionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("decommission/{entityName}", DecommissionService);
    }

    private static async Task<IResult> DecommissionService(
        [FromServices] ITestRunService testRunService,
        [FromServices] IEntitiesService entitiesService,
        [FromQuery(Name = "id")] string userId,
        [FromQuery(Name = "displayName")] string userDisplayName,
        string entityName, CancellationToken cancellationToken)
    {
        await testRunService.Decommission(entityName, cancellationToken);
        await entitiesService.SetDecommissionDetail(entityName, userId, userDisplayName, cancellationToken);
        return Results.Ok();
    }
}