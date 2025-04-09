using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{
    private const string RepositoriesBaseRoute = "repositories";
    private const string GithubRepositoriesBaseRoute = "github-repo";

    public static void MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(RepositoriesBaseRoute,
                async (IRepositoryService repositoryService, [FromQuery(Name = "team")] string? team,
                        [FromQuery(Name = "excludeTemplates")] bool? excludeTemplates,
                        CancellationToken cancellationToken) =>
                    await GetAllRepositories(repositoryService, team, excludeTemplates, cancellationToken));

        // Service repositories
        app.MapGet($"{RepositoriesBaseRoute}/services",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Service,
                        cancellationToken));

        // Service repository
        app.MapGet($"{RepositoriesBaseRoute}/services/{{id}}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Service, cancellationToken));

        // Test repositories
        app.MapGet($"{RepositoriesBaseRoute}/tests",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Test, cancellationToken));

        // Test repository
        app.MapGet($"{RepositoriesBaseRoute}/tests/{{id}}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Test, cancellationToken));

        // Template repositories
        app.MapGet($"{RepositoriesBaseRoute}/templates",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Template, cancellationToken));

        // Template repository
        app.MapGet($"{RepositoriesBaseRoute}/templates/{{id}}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Template, cancellationToken));

        // Library repositories
        app.MapGet($"{RepositoriesBaseRoute}/libraries",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Library, cancellationToken));

        // Library repository
        app.MapGet($"{RepositoriesBaseRoute}/libraries/{{id}}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Library, cancellationToken));

        // Get a Teams repositories
        app.MapGet($"{RepositoriesBaseRoute}/all/{{teamId}}", GetTeamRepositories);

        // Get a Teams Test repositories
        app.MapGet($"{RepositoriesBaseRoute}/all/tests/{{teamId}}", GetTeamTestRepositories);

        app.MapGet($"{RepositoriesBaseRoute}/{{id}}", GetRepositoryById);
    }

    private static async Task<IResult> GetRepositoryById(IRepositoryService repositoryService, string id,
        CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryById(id, cancellationToken);
        return maybeRepository == null
            ? Results.NotFound(Results.NotFound(new ApiError($"{id} not found")))
            : Results.Ok(new SingleRepositoryResponse(maybeRepository));
    }

    private static async Task<IResult> GetAllRepositories(IRepositoryService repositoryService, string? team,
        bool? excludeTemplates, CancellationToken cancellationToken)
    {
        var repositories = string.IsNullOrWhiteSpace(team)
            ? await repositoryService.AllRepositories(excludeTemplates.GetValueOrDefault(), cancellationToken)
            : await repositoryService.FindRepositoriesByGitHubTeam(team, excludeTemplates.GetValueOrDefault(),
                cancellationToken);

        if (excludeTemplates.GetValueOrDefault()) repositories = repositories.Where(r => !r.IsTemplate).ToList();

        return Results.Ok(new MultipleRepositoriesResponse(repositories));
    }

    private static async Task<IResult> GetRepositoryWithTopicById(IRepositoryService repositoryService, string id,
        CdpTopic topic, CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryWithTopicById(topic, id, cancellationToken);
        return maybeRepository == null
            ? Results.NotFound(new ApiError($"{id} not found"))
            : Results.Ok(new SingleRepositoryResponse(maybeRepository));
    }

    private static async Task<IResult> GetRepositoriesByTopic(IRepositoryService repositoryService, CdpTopic topic,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindRepositoriesByTopic(topic, cancellationToken);

        return Results.Ok(new MultipleRepositoriesResponse(repositories));
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
        var tests = await repositoryService.FindTeamRepositoriesByTopic(teamId, CdpTopic.Test,
            cancellationToken);

        return Results.Ok(new MultipleRepositoriesResponse(tests));
    }

    public sealed record MultipleRepositoriesResponse(string Message, IEnumerable<Repository> Repositories)
    {
        public MultipleRepositoriesResponse(IEnumerable<Repository> repositories) : this("success", repositories)
        {
        }
    }

    public sealed record AllRepoTemplatesLibrariesResponse(string Message, IEnumerable<Repository> Repositories,
        IEnumerable<Repository> Templates, IEnumerable<Repository> Libraries)
    {
        public AllRepoTemplatesLibrariesResponse(IEnumerable<Repository> repositories,
            IEnumerable<Repository> templates) : this("success", repositories, templates,
            Enumerable.Empty<Repository>())
        {
        }
    }

    public sealed record AllTeamRepositoriesResponse(string Message, IEnumerable<Repository> Libraries,
        IEnumerable<Repository> Services,
        IEnumerable<Repository> Templates, IEnumerable<Repository> Tests)
    {
        public AllTeamRepositoriesResponse(IEnumerable<Repository> libraries, IEnumerable<Repository> services,
            IEnumerable<Repository> templates, IEnumerable<Repository> tests) : this("success", libraries, services,
            templates, tests)
        {
        }
    }

    public sealed record SingleRepositoryResponse(string Message, Repository Repository)
    {
        public SingleRepositoryResponse(Repository repository) : this("success", repository)
        {
        }
    }
}