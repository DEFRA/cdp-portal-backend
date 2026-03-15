using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Microsoft.AspNetCore.Http.HttpResults;
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

    private static async Task<Results<NoContent, Ok<AutoTestRunTriggerResponse>>> FindForServiceName(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        string serviceName, CancellationToken cancellationToken)
    {
        var result = await autoTestRunTriggerService.FindForService(serviceName, cancellationToken);

        return result == null
            ? TypedResults.NoContent()
            : TypedResults.Ok(ToResponse(result));
    }

    private static async Task<Results<Accepted, Created<AutoTestRunTrigger>>> Create([FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTriggerDto trigger, CancellationToken cancellationToken)
    {
        var createdTrigger = await autoTestRunTriggerService.SaveTrigger(trigger, cancellationToken);
        return createdTrigger == null
            ? TypedResults.Accepted("auto-test-runs")
            : TypedResults.Created
                ($"auto-test-runs/{createdTrigger.ServiceName}", createdTrigger);
    }

    private static async Task<Results<NoContent, Ok<AutoTestRunTrigger>>> RemoveTestRun(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTriggerDto trigger, CancellationToken cancellationToken)
    {
        var updatedTrigger = await autoTestRunTriggerService.RemoveTestRun(trigger, cancellationToken);
        return updatedTrigger == null
            ? TypedResults.NoContent()
            : TypedResults.Ok(updatedTrigger);
    }

    private static async Task<Results<NoContent, Ok<AutoTestRunTrigger>>> UpdateTestRun(
        [FromServices] IAutoTestRunTriggerService autoTestRunTriggerService,
        AutoTestRunTriggerDto trigger, CancellationToken cancellationToken)
    {
        var updatedTrigger = await autoTestRunTriggerService.UpdateTestRun(trigger, cancellationToken);
        return updatedTrigger == null
            ? TypedResults.NoContent()
            : TypedResults.Ok(updatedTrigger);
    }

    private static AutoTestRunTriggerResponse ToResponse(AutoTestRunTrigger result)
    {
        return new AutoTestRunTriggerResponse(
            result.Created,
            result.ServiceName,
            result.TestSuites.OrderBy(ts => ts.Key).ToDictionary(ts => ts.Key, ts => ts.Value));
    }

    private sealed record AutoTestRunTriggerResponse(
        DateTime Created,
        string ServiceName,
        Dictionary<string, List<TestSuiteRunConfig>> TestSuites);
}
