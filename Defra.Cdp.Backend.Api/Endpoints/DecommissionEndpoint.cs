using Defra.Cdp.Backend.Api.Services.Deployments;
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
        [FromServices] IDeploymentsServiceV2 deploymentsServiceV2,
        [FromServices] ITestRunService testRunService, String serviceName, CancellationToken cancellationToken)
    {
        await deployableArtifactsService.Decommission(serviceName, cancellationToken);
        await deploymentsServiceV2.Decommission(serviceName, cancellationToken);
        await testRunService.Decommission(serviceName, cancellationToken);
        return Results.Ok();
    }
}