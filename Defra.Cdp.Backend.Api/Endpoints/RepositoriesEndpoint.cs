using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{
    private const string RepositoriesBaseRoute = "repositories";

    [Obsolete("Use entities")]
    public static void MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/repositories",
                async (IRepositoryService repositoryService, [FromQuery(Name = "team")] string? team,
                        [FromQuery(Name = "excludeTemplates")] bool? excludeTemplates,
                        CancellationToken cancellationToken) =>
                    await GetAllRepositories(repositoryService, team, excludeTemplates, cancellationToken));

        // Service repositories
        app.MapGet("/repositories/services",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Service,
                        cancellationToken));

        // Service repository
        app.MapGet("/repositories/services/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Service, cancellationToken));

        // Test repositories
        app.MapGet("/repositories/tests",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Test, cancellationToken));

        // Test repository
        app.MapGet("/repositories/tests/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Test, cancellationToken));

        // Template repositories
        app.MapGet("/repositories/templates",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Template, cancellationToken));

        // Template repository
        app.MapGet("/repositories/templates/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Template, cancellationToken));

        // Library repositories
        app.MapGet("/repositories/libraries",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Library, cancellationToken));

        // Library repository
        app.MapGet("/repositories/libraries/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Library, cancellationToken));

        // Get a Teams repositories
        app.MapGet("/repositories/all/{teamId}", GetTeamRepositories);

        // Get a Teams Test repositories
        app.MapGet("/repositories/all/tests/{teamId}", GetTeamTestRepositories);

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

    private static async Task<IResult> GetAllRepositories(IRepositoryService repositoryService, string? team,
        bool? excludeTemplates, CancellationToken cancellationToken)
    {
        var repositories = string.IsNullOrWhiteSpace(team)
            ? await repositoryService.AllRepositories(excludeTemplates.GetValueOrDefault(), cancellationToken)
            : await repositoryService.FindRepositoriesByGitHubTeam(team, excludeTemplates.GetValueOrDefault(),
                cancellationToken);

        if (excludeTemplates.GetValueOrDefault()) repositories = repositories.Where(r => !r.IsTemplate).ToList();

        return Results.Ok(repositories);
    }

    private static async Task<IResult> GetRepositoryWithTopicById(IRepositoryService repositoryService, string id,
        CdpTopic topic, CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryWithTopicById(topic, id, cancellationToken);
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

        var services = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Service,
            cancellationToken);

        var templates = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Template,
            cancellationToken);

        var tests = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Test,
            cancellationToken);

        return Results.Ok(new AllTeamRepositoriesResponse(libraries, services, templates, tests));
    }

    private static async Task<IResult> GetTeamTestRepositories(IRepositoryService repositoryService, string teamId,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Test,
            cancellationToken);

        return Results.Ok(repositories);
    }

    public sealed record AllTeamRepositoriesResponse(
        IEnumerable<Repository> Libraries,
        IEnumerable<Repository> Services,
        IEnumerable<Repository> Templates,
        IEnumerable<Repository> Tests);
}