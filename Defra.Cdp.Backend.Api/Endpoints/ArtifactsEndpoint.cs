using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsEndpoint
{
    private const string ArtifactsBaseRoute = "artifacts";
    private const string DeployablesBaseRoute = "deployables";
    private const string FilesBaseRoute = "files";
    private const string ServicesBaseRoute = "services";
    private const string Tag = "Artifacts";

    public static IEndpointRouteBuilder MapDeployablesEndpoint(this IEndpointRouteBuilder app)
    {
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
    private static async Task<IResult> ListRepos(IDeployablesService deployablesService)
    {
        var allRepos = await deployablesService.FindAll();
        return Results.Ok(allRepos);
    }

    // GET /artifacts/{repo}
    private static async Task<IResult> ListImagesForRepo(IDeployablesService deployablesService, string repo)
    {
        var allRepos = await deployablesService.FindAll(repo);
        return Results.Ok(allRepos);
    }

    // GET /artifacts/{repo}/{tag}
    private static async Task<IResult> ListImage(IDeployablesService deployablesService, string repo, string tag)
    {
        var image = await deployablesService.FindByTag(repo, tag);
        return image == null ? Results.NotFound(new { Message = $"{repo}:{tag} was not found" }) : Results.Ok(image);
    }

    // GET /files/{layer}
    private static async Task<IResult> GetFileContent(ILayerService layerService, string layer, string path)
    {
        var image = await layerService.FindFileAsync(layer, path);
        return image?.Content == null
            ? Results.NotFound(new { Message = $"{layer}/{path} was not found" })
            : Results.Ok(image.Content);
    }


    // GET /deployables
    private static async Task<IResult> ListDeployables(IDeployablesService deployablesService)
    {
        var repoNames = await deployablesService.FindAllRepoNames();
        return Results.Ok(repoNames);
    }

    // GET deployables/{repo}
    private static async Task<IResult> ListAvailableTagsForRepo(IDeployablesService deployablesService,
        string repo)
    {
        var tags = await deployablesService.FindAllTagsForRepo(repo);
        tags.Sort((a, b) =>
        {
            var la = SemVer.SemVerAsLong(a);
            var lb = SemVer.SemVerAsLong(b);
            if (la == lb) return 0;
            if (la > lb) return -1;

            return 1;
        });
        return Results.Ok(tags);
    }

    // GET /services
    private static async Task<IResult> ListAllServices(IDeployablesService deployablesService)
    {
        var services = await deployablesService.FindAllServices();
        return Results.Ok(services);
    }

    // GET /services/{service}
    private static async Task<ServiceInfo?> ListService(IDeployablesService deployablesService, string service)
    {
        return await deployablesService.FindServices(service);
    }
    
    // POST /artifacts/placeholder
    private static async Task<IResult> CreatePlaceholder(IDeployablesService deployablesService, string service, string githubUrl)
    {
        await deployablesService.CreatePlaceholderAsync(service, githubUrl);
        return Results.Ok();
    }
}