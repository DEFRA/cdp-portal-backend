using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    private const string AdminBaseRoute = "admin";
    private const string AdminTag = "Admin";

    public static IEndpointRouteBuilder MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{AdminBaseRoute}/backfill", Backfill)
            .WithName("PostAdminBackfill")
            .Produces<IEnumerable<DeployableArtifact>>() //Todo change 
            .WithTags(AdminTag);

        app.MapPost($"{AdminBaseRoute}/scan", RescanImageRequest)
            .WithName("PostRescanImage")
            .Produces<DeployableArtifact>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags(AdminTag);

        return app;
    }

    // POST /admin/backfill
    private static async Task<IResult> Backfill(EcsEventListener eventListener, IArtifactScanner scanner,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoint");
        logger.LogInformation("Starting back-fill operation");
        await eventListener.Backfill();
        var rescanRequest = await scanner.Backfill();
        var deployables = await Task.WhenAll(rescanRequest.Select(d => RescanImage(scanner, d.Repo, d.Tag)));
        return Results.Ok(deployables);
    }

    // POST /admin/scan?repo={repo}&tag={tag}
    private static async Task<IResult> RescanImageRequest(IArtifactScanner scanner, string repo, string tag)
    {
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace("tag"))
            return Results.BadRequest(new { errorMessage = "repo and tag must be specified" });
        var deployable = await RescanImage(scanner, repo, tag);
        return Results.Ok(deployable);
    }

    private static async Task<DeployableArtifact?> RescanImage(IArtifactScanner scanner, string repo, string tag)
    {
        var deployable = await scanner.ScanImage(repo, tag);
        return deployable;
    }
}