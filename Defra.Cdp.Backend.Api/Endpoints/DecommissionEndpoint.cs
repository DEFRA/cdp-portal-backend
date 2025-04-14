using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
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
        [FromServices] IDeploymentsService deploymentsService,
        [FromServices] ITestRunService testRunService,
        [FromServices] ITenantServicesService tenantServicesService,
         String serviceName, CancellationToken cancellationToken)
   {
      await deployableArtifactsService.Decommission(serviceName, cancellationToken);
      await testRunService.Decommission(serviceName, cancellationToken);
      return Results.Ok();
   }
}
