using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TestSuiteEndpoint
{

    public static IEndpointRouteBuilder MapTestSuiteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("test-run/{runId}", FindTestRun);
        app.MapGet("test-run", FindTestRunsForSuite); // filter by test e.g. /test-run?name=foo-tests 
        app.MapPost("test-run", CreateTestRun);
        app.MapGet("test-suite", FindAllTestSuites);
        app.MapGet("test-suite/{name}", FindTestSuites);
        return app;
    }

    static async Task<IResult> FindTestRun( [FromServices] ITestRunService testRunService, string runId, CancellationToken cancellationToken)
    {
        var result = await testRunService.FindTestRun(runId, cancellationToken);
        return result == null ? Results.NotFound() : Results.Ok(result);
    }

    static async Task<IResult> FindTestRunsForSuite( [FromServices] ITestRunService testRunService, string name,
        CancellationToken cancellationToken)
    {
        var result = await testRunService.FindTestRunsForTestSuite(name, 100, cancellationToken);
        return Results.Ok(result);
    }

    static async Task<IResult> CreateTestRun( [FromServices] ITestRunService testRunService, TestRun testRun, CancellationToken cancellationToken)
    {
        await testRunService.CreateTestRun(testRun, cancellationToken);
        return Results.Created($"test-run/{testRun.RunId}", null);
    }
    
    static async Task<IResult> FindAllTestSuites(IDeployablesService deployablesService,
        CancellationToken cancellationToken)
    {
        var testSuites = await deployablesService.FindAllServices(ArtifactRunMode.Job, cancellationToken);
        return Results.Ok(testSuites);
    }
    
    static async Task<IResult> FindTestSuites(string name, IDeployablesService deployablesService, CancellationToken cancellationToken)
    {
        var testSuite = await deployablesService.FindServices(name, cancellationToken);
        return testSuite == null ? Results.NotFound() : Results.Ok(testSuite);
    }
}
