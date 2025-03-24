using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AutoTestRunTriggerEndpoint
{
    public static void MapAutoTestRunTriggerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("auto-test-runs/{serviceName}", FindForServiceName);
        app.MapPost("auto-test-runs", Save);
    }

    private static async Task<IResult> FindForServiceName( [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService, string serviceName, CancellationToken cancellationToken)
    {
        var result = await autoTestRunTriggerService.FindForService(serviceName, cancellationToken);
        return result == null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Save( [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService, AutoTestRunTrigger trigger, CancellationToken cancellationToken)
    {
        var persistedTrigger = await autoTestRunTriggerService.PersistTrigger(trigger, cancellationToken);
        return persistedTrigger == null ? Results.Accepted("auto-test-runs") : Results.Created($"auto-test-runs/{persistedTrigger.ServiceName}", persistedTrigger);
    }
}
