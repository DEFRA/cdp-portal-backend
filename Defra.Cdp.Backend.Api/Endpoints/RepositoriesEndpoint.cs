using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{

    public static void MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        // Template repositories
        app.MapGet("/repositories/templates",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Template, cancellationToken));

        // Template repository
        app.MapGet("/repositories/templates/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryById(repositoryService, id, cancellationToken));

        // Library repositories
        app.MapGet("/repositories/libraries",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Library, cancellationToken));

        // Library repository
        app.MapGet("/repositories/libraries/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryById(repositoryService, id, cancellationToken));

        // Get a Teams repositories
        app.MapGet("/repositories/all/{teamId}", GetTeamRepositories);

        app.MapGet("/repositories/{id}", GetRepositoryById);
    }

    private static async Task<IResult> GetRepositoryById(IRepositoryService repositoryService, string id,
        CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryById(id, cancellationToken);
        return maybeRepository == null
            ? Results.NotFound(new ApiError($"{id} not found"))
            : Results.Ok(maybeRepository);
    }

    private static async Task<IResult> GetRepositoriesByTopic(IRepositoryService repositoryService, CdpTopic topic,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindRepositoriesByTopic(topic, cancellationToken);

        return Results.Ok(repositories);
    }

    private static async Task<IResult> GetTeamRepositories(IRepositoryService repositoryService, string teamId,
        CancellationToken cancellationToken)
    {
        var libraries = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Library,
            cancellationToken);

        var templates = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Template,
            cancellationToken);

        return Results.Ok(new AllTeamRepositoriesResponse(libraries, templates));
    }

    public sealed record AllTeamRepositoriesResponse(
        IEnumerable<Repository> Libraries,
        IEnumerable<Repository> Templates);
}