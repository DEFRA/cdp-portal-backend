using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver.Linq;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsEndpoint
{
    private const string ArtifactsBaseRoute = "artifacts";
    private const string DeployablesBaseRoute = "deployables";
    private const string EnvironmentsBaseRoute = "environments";
    private const string FilesBaseRoute = "files";
    private const string ServicesBaseRoute = "services";
    private const string Tag = "Artifacts";
    private static ILogger _logger = default!;

    public static IEndpointRouteBuilder MapDeployablesEndpoint(this IEndpointRouteBuilder app, ILogger logger)
    {
        _logger = logger;

        app.MapGet(ArtifactsBaseRoute, ListRepos)
            .WithName("GetAllImages")
            .Produces<List<DeployableArtifact>>()
            .WithTags(Tag);

        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}", ListImagesForRepo)
            .WithName("GetImagesForRepo")
            .Produces<List<DeployableArtifact>>()
            .WithTags(Tag);

        app.MapGet($"{ArtifactsBaseRoute}/{{repo}}/{{tag}}", ListImage)
            .WithName("GetImagesForRepoAndTag")
            .Produces<List<DeployableArtifact>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(Tag);

        app.MapGet($"{FilesBaseRoute}/{{layer}}", GetFileContent)
            .WithName("GetFileContent")
            .Produces<LayerFile>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(Tag);

        app.MapGet(DeployablesBaseRoute, ListDeployables)
            .WithName("GetDeployables")
            .Produces<List<string>>()
            .WithTags(Tag);

        app.MapGet($"{DeployablesBaseRoute}/{{repo}}", ListAvailableTagsForRepo)
            .WithName("GetTagsForRepo")
            .Produces<List<string>>()
            .WithTags(Tag);

        app.MapGet(EnvironmentsBaseRoute, DeployableEnvironments)
            .RequireAuthorization()
            .WithName("GetEnvironments")
            .Produces<List<string>>()
            .WithTags(Tag);

        app.MapGet(ServicesBaseRoute, ListAllServices)
            .WithName("GetServices")
            .Produces<List<ServiceInfo>>()
            .WithTags(Tag);

        app.MapGet($"{ServicesBaseRoute}/{{service}}", ListService)
            .WithName("GetService")
            .Produces<ServiceInfo>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(Tag);

        app.MapPost($"{ArtifactsBaseRoute}/placeholder", CreatePlaceholder)
            .WithName("CreatePlaceholder")
            .Produces(StatusCodes.Status200OK)
            .WithTags(Tag);

        return app;
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
        var image = await deployablesService.FindByTag(repo, tag, cancellationToken);
        return image == null ? Results.NotFound(new { Message = $"{repo}:{tag} was not found" }) : Results.Ok(image);
    }

    // GET /files/{layer}
    private static async Task<IResult> GetFileContent(ILayerService layerService, string layer, string path,
        CancellationToken cancellationToken)
    {
        var image = await layerService.FindFileAsync(layer, path, cancellationToken);
        return image?.Content == null
            ? Results.NotFound(new { Message = $"{layer}/{path} was not found" })
            : Results.Ok(image.Content);
    }

    
    // GET /deployables
    private static async Task<IResult> ListDeployables(
        IDeployablesService deployablesService,
        IConfiguration configuration, 
        HttpContext httpContext,
        ILoggerFactory loggerFactory, 
        [FromQuery] string? type, 
        CancellationToken cancellationToken)
    {
        List<string> groups = httpContext.Request.Query["groups"].Where(g => g != null).ToList()!;
        var adminGroup = configuration.GetValue<string>("AzureAdminGroupId")!;

        if (!Enum.TryParse(type ?? ArtifactRunMode.Service.ToString(), true, out ArtifactRunMode runMode))
        {
            return Results.BadRequest("Invalid type parameter, requires either: [service, job]");
        }

        var repoNames = groups!.Contains(adminGroup)
            ? await deployablesService.FindAllRepoNames(runMode, cancellationToken)
            : await deployablesService.FindAllRepoNames(runMode, groups, cancellationToken);
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

    // Get /environments
    private static async Task<IResult> DeployableEnvironments(IDeployablesService deployablesService,
        IConfiguration configuration, HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        var groups = Helpers.ExtractGroups(httpContext, loggerFactory);
        if (groups.IsNullOrEmpty()) return Results.Forbid();
        var adminGroup = configuration.GetValue<string>("AzureAdminGroupId")!;
        var isAdmin = groups!.Contains(adminGroup);
        _logger.LogInformation("Grabbing deployable environments for {AdminOrTenant}", isAdmin ? "admin" : "tenant");
        return Results.Ok(await deployablesService.DeployableEnvironments(isAdmin));
    }

    // GET /services
    private static async Task<IResult> ListAllServices(IDeployablesService deployablesService,
        CancellationToken cancellationToken)
    {
        var services = await deployablesService.FindAllServices(cancellationToken);
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