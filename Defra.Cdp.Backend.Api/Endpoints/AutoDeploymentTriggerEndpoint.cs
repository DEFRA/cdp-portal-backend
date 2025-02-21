using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AutoDeploymentTriggerEndpoint
{
    public static void MapAutoDeploymentTriggerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("auto-deployments", FindAll);
        app.MapGet("auto-deployments/{serviceName}", FindForServiceName);
        app.MapPost("auto-deployments", Create);
    }

    static async Task<IResult> FindForServiceName( [FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService, string serviceName, CancellationToken cancellationToken)
    {
        var result = await autoDeploymentTriggerService.FindForServiceName(serviceName, cancellationToken);
        if (result == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> FindAll( [FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService, CancellationToken cancellationToken)
    {
        return Results.Ok(await autoDeploymentTriggerService.FindAll(cancellationToken));
    }

    static async Task<IResult> Create( [FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService, AutoDeploymentTrigger trigger, CancellationToken cancellationToken)
    {
        var persistedTrigger = await autoDeploymentTriggerService.PersistTrigger(trigger, cancellationToken);
        return persistedTrigger == null ? Results.Accepted("auto-deployments") : Results.Created($"auto-deployments/{persistedTrigger.ServiceName}", persistedTrigger);
    }
}
