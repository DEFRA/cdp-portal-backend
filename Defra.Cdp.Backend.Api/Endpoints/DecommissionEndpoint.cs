using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DecommissionEndpoint
{
    public static void MapDecommissionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("decommission/{serviceName}", DecommissionService);
    }

    static async Task<IResult> DecommissionService(
        [FromServices] IDeployableArtifactsService deployableArtifactsService,
        [FromServices] ITestRunService testRunService,
        [FromServices] IEntitiesService entitiesService,
        [FromQuery(Name = "id")] string userId,
        [FromQuery(Name = "displayName")] string userDisplayName,
         String serviceName, CancellationToken cancellationToken)
   {
      await deployableArtifactsService.Decommission(serviceName, cancellationToken);
      await testRunService.Decommission(serviceName, cancellationToken);
      await entitiesService.Decommission(serviceName, userId, userDisplayName, cancellationToken);
      return Results.Ok();
   }
}
