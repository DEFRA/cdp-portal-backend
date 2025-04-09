using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AutoTestRunTriggerEndpoint
{
    public static void MapAutoTestRunTriggerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("auto-test-runs/{serviceName}", FindForServiceName);
        app.MapPost("auto-test-runs", Create);
        app.MapPatch("auto-test-runs/remove-test-run", RemoveTestRun);
        app.MapPatch("auto-test-runs/update-test-run", UpdateTestRun);
    }

    private static async Task<IResult> FindForServiceName(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        string serviceName, CancellationToken cancellationToken)
    {
        var result = await autoTestRunTriggerService.FindForService(serviceName, cancellationToken);

        return result == null
            ? Results.NoContent()
            : Results.Ok(
                new
                {
                    result.Created,
                    result.ServiceName,
                    TestSuites = result.TestSuites.OrderBy(ts => ts.Key)
                        .ToDictionary(ts => ts.Key, ts => ts.Value)
                });
    }

    private static async Task<IResult> Create( [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTrigger trigger, CancellationToken cancellationToken)
    {
        var createdTrigger = await autoTestRunTriggerService.SaveTrigger(trigger, cancellationToken);
        return createdTrigger == null
            ? Results.Accepted("auto-test-runs")
            : Results.Created
                ($"auto-test-runs/{createdTrigger.ServiceName}", createdTrigger);
    }

    private static async Task<IResult> RemoveTestRun(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTrigger trigger, CancellationToken cancellationToken)
    {
        var updatedTrigger = await autoTestRunTriggerService.RemoveTestRun(trigger, cancellationToken);
        return updatedTrigger == null
            ? Results.NoContent()
            : Results.Ok(updatedTrigger);
    }

    private static async Task<IResult> UpdateTestRun(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTrigger trigger, CancellationToken cancellationToken)
    {
        var updatedTrigger = await autoTestRunTriggerService.UpdateTestRun(trigger, cancellationToken);
        return updatedTrigger == null
            ? Results.NoContent()
            : Results.Ok(updatedTrigger);
    }
}
