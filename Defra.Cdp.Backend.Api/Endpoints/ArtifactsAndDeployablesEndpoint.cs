using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsAndDeployablesEndpoint
{
    private const string ArtifactsBaseRoute = "artifacts";
    private const string DeployablesBaseRoute = "deployables";
    private const string FilesBaseRoute = "files";

    public static void MapArtifactsAndDeployablesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(ArtifactsBaseRoute, ListRepos);
        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}", ListImagesForRepo);
        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}/{{tag}}", ListImage);
        app.MapPost($"{ArtifactsBaseRoute}/{{repo}}/{{tag}}/annotations", AddAnnotation);
        app.MapDelete($"{ArtifactsBaseRoute}/{{repo}}/{{tag}}/annotations", DeleteAnnotations);
        app.MapGet($"{FilesBaseRoute}/{{layer}}", GetFileContent);
        app.MapGet(DeployablesBaseRoute, ListDeployables);
        app.MapGet($"{DeployablesBaseRoute}/{{repo}}", ListAvailableTagsForRepo);
        app.MapPost($"{ArtifactsBaseRoute}/placeholder", CreatePlaceholder);
    }

    // GET /artifacts
    private static async Task<IResult> ListRepos(IDeployableArtifactsService deployableArtifactsService,
        CancellationToken cancellationToken)
    {
        var allRepos = await deployableArtifactsService.FindAll(cancellationToken);
        return Results.Ok(allRepos);
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

    // POST /artifacts/{repo}/{tag}/annotations
    private static async Task<IResult> AddAnnotation(IDeployableArtifactsService deployableArtifactsService, string repo,
        string tag,
        IValidator<RequestedAnnotation> validator,
        RequestedAnnotation requestedAnnotation,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(requestedAnnotation, cancellationToken);
        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());

        await deployableArtifactsService.AddAnnotation(repo, tag, requestedAnnotation.Title.ToLower(), requestedAnnotation.Description, cancellationToken);
        var image = await deployableArtifactsService.FindByTag(repo, tag, cancellationToken);
        return image == null ? Results.NotFound(new ApiError($"{repo}:{tag} was not found")) : Results.Ok(image);
    }


    // DELETE /artifacts/{repo}/{tag}/annotations
    private static async Task<IResult> DeleteAnnotations(IDeployableArtifactsService deployableArtifactsService,
        string repo,
        string tag,
        [FromQuery(Name = "title")] string? title,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            await deployableArtifactsService.RemoveAnnotations(repo, tag, cancellationToken);
        }
        else
        {
            await deployableArtifactsService.RemoveAnnotation(repo, tag, title.ToLower(), cancellationToken);
        }

        var image = await deployableArtifactsService.FindByTag(repo, tag, cancellationToken);
        return image == null ? Results.NotFound(new ApiError($"{repo}:{tag} was not found")) : Results.Ok(image);
    }

    // GET /files/{layer}
    private static async Task<IResult> GetFileContent(ILayerService layerService, string layer, string path,
        CancellationToken cancellationToken)
    {
        var image = await layerService.FindFileAsync(layer, path, cancellationToken);
        return image?.Content == null
            ? Results.NotFound(new ApiError($"{layer}/{path} was not found"))
            : Results.Ok(image.Content);
    }


    // GET /deployables
    private static async Task<IResult> ListDeployables(
        IDeployableArtifactsService deployableArtifactsService,
        IConfiguration configuration,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        [FromQuery] string? runMode,
        CancellationToken cancellationToken)
    {
        List<string> groups = httpContext.Request.Query["groups"].Where(g => g != null).ToList()!;
        var adminGroup = configuration.GetValue<string>("AzureAdminGroupId")!;

        if (!Enum.TryParse(runMode ?? ArtifactRunMode.Service.ToString(), true, out ArtifactRunMode artifactRunMode))
        {
            return Results.BadRequest("Invalid type parameter, requires either: [service, job]");
        }

        var repoNames = groups.Contains(adminGroup)
            ? await deployableArtifactsService.FindAllRepoNames(artifactRunMode, cancellationToken)
            : await deployableArtifactsService.FindAllRepoNames(artifactRunMode, groups, cancellationToken);
        return Results.Ok(repoNames);
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
    
    // POST /artifacts/placeholder
    private static async Task<IResult> CreatePlaceholder(IDeployableArtifactsService deployableArtifactsService, string service,
        string githubUrl, string? runMode, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(runMode, true, out ArtifactRunMode mode))
        {
            mode = ArtifactRunMode.Service;
        }
        await deployableArtifactsService.CreatePlaceholderAsync(service, githubUrl, mode, cancellationToken);
        return Results.Ok();
    }
}