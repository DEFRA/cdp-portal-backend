using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    private const string AdminBaseRoute = "admin";

    public static void MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{AdminBaseRoute}/backfill", Backfill);
        app.MapPost($"{AdminBaseRoute}/scan", RescanImageRequest);
        app.MapGet($"{AdminBaseRoute}/entity/status", UpdateStatus);
    }

    /// <summary>
    /// Debugging endpoint: Forces an update of the entity creation status.
    /// </summary>
    /// <param name="entitiesService"></param>
    /// <returns></returns>
    private static async Task<IResult> UpdateStatus(IEntitiesService entitiesService)
    {
        await entitiesService.BulkUpdateTenantConfigStatus(CancellationToken.None);
        return Results.Ok();
    }

    // POST /admin/backfill
    private static async Task<IResult> Backfill(EcsEventListener eventListener, IArtifactScanner scanner,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoint");
        logger.LogInformation("Starting back-fill operation");
        await eventListener.BackFill(cancellationToken);
        var rescanRequest = await scanner.Backfill(cancellationToken);
        var deployables =
            await Task.WhenAll(rescanRequest.Select(d => RescanImage(scanner, d.Repo, d.Tag, cancellationToken)));
        return Results.Ok(deployables);
    }

    // POST /admin/scan?repo={repo}&tag={tag}
    private static async Task<IResult> RescanImageRequest(IArtifactScanner scanner, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string repo, string tag)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoint");
        logger.LogInformation("Rescanning {repo} {tag}", repo, tag);
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace("tag"))
            return Results.BadRequest(new { errorMessage = "repo and tag must be specified" });
        var deployable = await RescanImage(scanner, repo, tag, cancellationToken);
        return Results.Ok(deployable);
    }

    private static async Task<ArtifactScannerResult> RescanImage(IArtifactScanner scanner, string repo, string tag,
        CancellationToken cancellationToken)
    {
        var deployable = await scanner.ScanImage(repo, tag, cancellationToken);
        return deployable;
    }
}