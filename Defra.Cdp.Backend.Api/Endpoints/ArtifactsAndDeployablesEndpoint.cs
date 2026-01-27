using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsAndDeployablesEndpoint
{
   
    public static void MapArtifactsAndDeployablesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/artifacts/{repo}", ListImagesForRepo);
        app.MapGet("/artifacts/{repo}/{tag}", ListImage);
        app.MapGet("/latest-artifacts", ListLatestArtifacts);
        app.MapGet("/deployables/{repo}", ListAvailableTagsForRepo);
        
    }


    // GET /artifacts/{repo}
    private static async Task<IResult> ListImagesForRepo(IDeployableArtifactsService deployableArtifactsService, string repo,
        CancellationToken cancellationToken)
    {
        var allRepos = await deployableArtifactsService.FindAll(repo, cancellationToken);
        return Results.Ok(allRepos);
    }

    // GET /artifacts/{repo}/{tag}
    private static async Task<IResult> ListImage(IDeployableArtifactsService deployableArtifactsService, string repo, string tag,
        CancellationToken cancellationToken)
    {
        DeployableArtifact? image;
        if (tag == "latest")
        {
            image = await deployableArtifactsService.FindLatest(repo, cancellationToken);
        }
        else
        {
            image = await deployableArtifactsService.FindByTag(repo, tag, cancellationToken);
        }
        return image == null ? Results.NotFound(new ApiError($"{repo}:{tag} was not found")) : Results.Ok(image);
    }

    // GET deployables/{repo}
    private static async Task<IResult> ListAvailableTagsForRepo(IDeployableArtifactsService deployableArtifactsService,
        IConfiguration configuration, HttpContext httpContext,
        ILoggerFactory loggerFactory,
        string repo,
        CancellationToken cancellationToken)
    {
        var tags = await deployableArtifactsService.FindAllTagsForRepo(repo, cancellationToken);
        return Results.Ok(tags);
    }
    
    
    // GET latest-artifacts
    private static async Task<IResult> ListLatestArtifacts(
        IDeployableArtifactsService deployableArtifactsService,
        CancellationToken cancellationToken)
    {
        var tags = await deployableArtifactsService.FindLatestForAll(cancellationToken);
        return Results.Ok(tags);
    }
}