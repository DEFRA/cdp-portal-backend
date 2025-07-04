using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AutoDeploymentTriggerEndpoint
{
    public static void MapAutoDeploymentTriggerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("auto-deployments", FindAll);
        app.MapGet("auto-deployments/{serviceName}", FindForServiceName);
        app.MapPost("auto-deployments", Save);
    }

    private static async Task<IResult> FindForServiceName(
        [FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService, string serviceName,
        CancellationToken cancellationToken)
    {
        var result = await autoDeploymentTriggerService.FindForService(serviceName, cancellationToken);
        return result == null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> FindAll(
        [FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService, CancellationToken cancellationToken)
    {
        return Results.Ok(await autoDeploymentTriggerService.FindAll(cancellationToken));
    }

    private static async Task<IResult> Save([FromServices] IAutoDeploymentTriggerService autoDeploymentTriggerService,
        AutoDeploymentTrigger trigger, CancellationToken cancellationToken)
    {
        var persistedTrigger = await autoDeploymentTriggerService.PersistTrigger(trigger, cancellationToken);
        return persistedTrigger == null
            ? Results.Accepted("auto-deployments")
            : Results.Created($"auto-deployments/{persistedTrigger.ServiceName}", persistedTrigger);
    }
}