using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{

    public static void MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        // Template repositories
        app.MapGet("/repositories/templates", GetTemplateRepositories);

        app.MapGet("/repositories/templates/{id}", GetRepositoryById);

        // Library repositories
        app.MapGet("/repositories/libraries", GetLibraryRepositories);

        // Library repository
        app.MapGet("/repositories/libraries/{id}", GetRepositoryById);

        // Get a Teams repositories
        app.MapGet("/repositories/all/{teamId}", GetTeamRepositories);

        app.MapGet("/repositories/{id}", GetRepositoryById);
    }

    private static async Task<Results<NotFound<ApiError>,Ok<Repository>>> GetRepositoryById(IRepositoryService repositoryService, string id,
        CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryById(id, cancellationToken);
        return maybeRepository == null
            ? TypedResults.NotFound(new ApiError($"{id} not found"))
            : TypedResults.Ok(maybeRepository);
    }
    
    private static async Task<Ok<List<Repository>>> GetTemplateRepositories(IRepositoryService repositoryService,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindRepositoriesByTopic(CdpTopic.Template, cancellationToken);

        return TypedResults.Ok(repositories);
    }
    
    private static async Task<Ok<List<Repository>>> GetLibraryRepositories(IRepositoryService repositoryService,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindRepositoriesByTopic(CdpTopic.Library, cancellationToken);

        return TypedResults.Ok(repositories);
    }

    private static async Task<Ok<AllTeamRepositoriesResponse>> GetTeamRepositories(IRepositoryService repositoryService, string teamId,
        CancellationToken cancellationToken)
    {
        var libraries = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Library,
            cancellationToken);

        var templates = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Template,
            cancellationToken);

        return TypedResults.Ok(new AllTeamRepositoriesResponse(libraries, templates));
    }

    public sealed record AllTeamRepositoriesResponse(
        IEnumerable<Repository> Libraries,
        IEnumerable<Repository> Templates);
}