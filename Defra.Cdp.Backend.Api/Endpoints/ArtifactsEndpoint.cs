using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsEndpoint
{
    private const string ArtifactsBaseRoute = "artifacts";
    private const string DeployablesBaseRoute = "deployables";
    private const string FilesBaseRoute = "files";
    private const string ServicesBaseRoute = "services";
    private static ILogger _logger = default!;

    public static void MapDeployablesEndpoint(this IEndpointRouteBuilder app, ILogger logger)
    {
        _logger = logger;

        app.MapGet(ArtifactsBaseRoute, ListRepos);
        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}", ListImagesForRepo);
        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}/{{tag}}", ListImage);
        app.MapGet($"{FilesBaseRoute}/{{layer}}", GetFileContent);
        app.MapGet(DeployablesBaseRoute, ListDeployables);
        app.MapGet($"{DeployablesBaseRoute}/{{repo}}", ListAvailableTagsForRepo);
        app.MapGet(ServicesBaseRoute, ListAllServices);
        app.MapGet($"{ServicesBaseRoute}/{{service}}", ListService);
        app.MapPost($"{ArtifactsBaseRoute}/placeholder", CreatePlaceholder);
    }

    // GET /artifacts
    private static async Task<IResult> ListRepos(IDeployablesService deployablesService,
        CancellationToken cancellationToken)
    {
        var allRepos = await deployablesService.FindAll(cancellationToken);
        return Results.Ok(allRepos);
    }

    // GET /artifacts/{repo}
    private static async Task<IResult> ListImagesForRepo(IDeployablesService deployablesService, string repo,
        CancellationToken cancellationToken)
    {
        var allRepos = await deployablesService.FindAll(repo, cancellationToken);
        return Results.Ok(allRepos);
    }

    // GET /artifacts/{repo}/{tag}
    private static async Task<IResult> ListImage(IDeployablesService deployablesService, string repo, string tag,
        CancellationToken cancellationToken)
    {
        DeployableArtifact? image;
        if (tag == "latest")
        {
            image = await deployablesService.FindLatest(repo, cancellationToken);
        }
        else
        {
            image = await deployablesService.FindByTag(repo, tag, cancellationToken);    
        }
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
        IDeployablesService deployablesService,
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
            ? await deployablesService.FindAllRepoNames(artifactRunMode, cancellationToken)
            : await deployablesService.FindAllRepoNames(artifactRunMode, groups, cancellationToken);
        return Results.Ok(repoNames);
    }

    // GET deployables/{repo}
    private static async Task<IResult> ListAvailableTagsForRepo(IDeployablesService deployablesService,
        IConfiguration configuration, HttpContext httpContext,
        ILoggerFactory loggerFactory,
        string repo,
        CancellationToken cancellationToken)
    {
        var tags = await deployablesService.FindAllTagsForRepo(repo, cancellationToken);
        return Results.Ok(tags);
    }

    // GET /services
    private static async Task<IResult> ListAllServices(IDeployablesService deployablesService,
        CancellationToken cancellationToken)
    {
        var services = await deployablesService.FindAllServices(ArtifactRunMode.Service, cancellationToken);
        return Results.Ok(services);
    }

    // GET /services/{service}
    private static async Task<ServiceInfo?> ListService(IDeployablesService deployablesService, string service,
        CancellationToken cancellationToken)
    {
        return await deployablesService.FindServices(service, cancellationToken);
    }

    // POST /artifacts/placeholder
    private static async Task<IResult> CreatePlaceholder(IDeployablesService deployablesService, string service,
        string githubUrl, string? runMode, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(runMode, true, out ArtifactRunMode mode))
        {
            mode = ArtifactRunMode.Service;
        }
        await deployablesService.CreatePlaceholderAsync(service, githubUrl, mode, cancellationToken);
        return Results.Ok();
    }
}