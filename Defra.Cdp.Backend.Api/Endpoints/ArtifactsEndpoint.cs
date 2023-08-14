using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Artifacts;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ArtifactsEndpoint
{
    private const string ArtifactsBaseRoute = "artifacts";
    private const string DeployablesBaseRoute = "deployables";
    private const string FilesBaseRoute = "files";
    private const string ServicesBaseRoute = "files";
    private const string Tag = "Artifacts";

    // Todo add 404 for when repo does not exist
    public static IEndpointRouteBuilder MapDeployments(this IEndpointRouteBuilder app)
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
            .WithTags(Tag);

        app.MapGet($"{FilesBaseRoute}/{{layer}}", GetFileContent)
            .WithName("GetFileContent")
            .Produces<LayerFile>()
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
        return image == null ? Results.NotFound($"{repo}:{tag} was not found") : Results.Ok(image);
    }

    // GET /files/{layer}
    private static async Task<IResult> GetFileContent(ILayerService layerService, string layer, string path)
    {
        var image = await layerService.FindFileAsync(layer, path);
        if (image?.Content == null) return Results.NotFound($"{layer}/{path} was not found");
        return Results.Ok(image.Content);
    }


    // GET /deployables
    private static async Task<IResult> ListDeployables(IDeployablesService deployablesService)
    {
        var repoNames = await deployablesService.FindAllRepoNames();
        return Results.Ok(repoNames);
    }

    // [HttpGet]
    // [Route("deployables/{repo}")]
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

    // [HttpGet]
    // [Route("services")]
    private static async Task<IResult> ListAllServices(IDeployablesService deployablesService)
    {
        var services = await deployablesService.FindAllServices();
        return Results.Ok(services);
    }

    //
    // [HttpGet]
    // [Route("services/{service}")]
    private static async Task<ServiceInfo?> ListService(IDeployablesService deployablesService, string service)
    {
        return await deployablesService.FindServices(service);
    }
}