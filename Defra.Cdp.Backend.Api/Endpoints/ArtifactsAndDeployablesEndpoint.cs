using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.AspNetCore.Http.HttpResults;

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
    private static async Task<Ok<List<DeployableArtifact>>> ListImagesForRepo(IDeployableArtifactsService deployableArtifactsService, string repo,
        CancellationToken cancellationToken)
    {
        var allRepos = await deployableArtifactsService.FindAll(repo, cancellationToken);
        return TypedResults.Ok(allRepos);
    }

    // GET /artifacts/{repo}/{tag}
    private static async Task<Results<NotFound<ApiError>, Ok<DeployableArtifact>>> ListImage(IDeployableArtifactsService deployableArtifactsService, string repo, string tag,
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
        return image == null
            ? TypedResults.NotFound(new ApiError($"{repo}:{tag} was not found"))
            : TypedResults.Ok(image);
    }

    // GET deployables/{repo}
    private static async Task<Ok<List<TagInfo>>> ListAvailableTagsForRepo(IDeployableArtifactsService deployableArtifactsService,
        IConfiguration configuration, HttpContext httpContext,
        ILoggerFactory loggerFactory,
        string repo,
        CancellationToken cancellationToken)
    {
        var tags = await deployableArtifactsService.FindAllTagsForRepo(repo, cancellationToken);
        return TypedResults.Ok(tags);
    }
    
    
    // GET latest-artifacts
    private static async Task<Ok<List<ArtifactVersion>>> ListLatestArtifacts(
        IDeployableArtifactsService deployableArtifactsService,
        CancellationToken cancellationToken)
    {
        var tags = await deployableArtifactsService.FindLatestForAll(cancellationToken);
        return TypedResults.Ok(tags);
    }
}
