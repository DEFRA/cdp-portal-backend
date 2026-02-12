using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TestSuiteEndpoint
{
    public static void MapTestSuiteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/test-run/{runId}", FindTestRun);
        app.MapGet("/test-run", FindTestRunsForSuite); // filter by test e.g. /test-run?name=foo-tests 
        app.MapPost("/test-run", CreateTestRun);
    }

    private static async Task<IResult> FindTestRun([FromServices] ITestRunService testRunService, string runId,
        CancellationToken cancellationToken)
    {
        var result = await testRunService.FindTestRun(runId, cancellationToken);
        if (result == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> FindTestRunsForSuite([FromServices] ITestRunService testRunService,
        [FromQuery(Name = "name")] string? name,
        [FromQuery(Name = "environment")] string? environment,
        [FromQuery(Name = "testStatus")] string[]? testStatus,
        [FromQuery(Name = "taskStatus")] string[]? taskStatus,
        [FromQuery(Name = "start")] DateTime? start,
        [FromQuery(Name = "end")] DateTime? end,
        [FromQuery(Name = "offset")] int? offset,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "size")] int? size,
        CancellationToken cancellationToken = default)
    {
        var matcher = new TestRunMatcher
        {
            Name = name,
            Environment = environment,
            TaskStatus = taskStatus,
            TestStatus = testStatus,
            Start = start,
            End = end,
        };

        var result = await testRunService.FindTestRuns(matcher, offset ?? 0,
            page ?? TestRunService.DefaultPage,
            size ?? TestRunService.DefaultPageSize, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateTestRun([FromServices] ITestRunService testRunService, TestRun testRun,
        CancellationToken cancellationToken)
    {
        await testRunService.CreateTestRun(testRun, cancellationToken);
        return Results.Created($"test-run/{testRun.RunId}", null);
    }
}