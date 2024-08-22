using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{
    public static IEndpointRouteBuilder MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("repositories",
            async (IRepositoryService repositoryService, [FromQuery(Name = "team")] string? team,
                    [FromQuery(Name = "excludeTemplates")] bool? excludeTemplates,
                    CancellationToken cancellationToken) =>
                await GetAllRepositories(repositoryService, team, excludeTemplates, cancellationToken));
        
        // Service repositories
        app.MapGet("repositories/services",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Service,
                        cancellationToken));

        // Service repository
        app.MapGet("repositories/services/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Service, cancellationToken));

        // Test repositories
        app.MapGet("repositories/tests",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Test, cancellationToken));

        // Test repository
        app.MapGet("repositories/tests/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Test, cancellationToken));

        // Template repositories
        app.MapGet("repositories/templates",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Template, cancellationToken));

        // Template repository
        app.MapGet("repositories/templates/{id}",
                async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                    await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Template, cancellationToken));

        // Library repositories
        app.MapGet("repositories/libraries",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, CdpTopic.Library, cancellationToken));

        // Library repository
        app.MapGet("repositories/libraries/{id}",
            async (IRepositoryService repositoryService, string id, CancellationToken cancellationToken) =>
                await GetRepositoryWithTopicById(repositoryService, id, CdpTopic.Library, cancellationToken));

        // Get a Teams repositories
        app.MapGet("repositories/all/{teamId}", GetTeamRepositories);

        app.MapGet("repositories/{id}", GetRepositoryById);

        // ALL THE THINGS
        app.MapGet("github-repo/{team}", GetAllReposTemplatesLibraries);

        // return as templates in the body
        app.MapGet("templates",
                async (ITemplatesService templatesService, [FromQuery(Name = "team")] string? team,
                        CancellationToken cancellationToken) =>
                    await GetAllTemplates(templatesService, team, cancellationToken));

        app.MapGet("templates/{templateId}", GetTemplateById);

        app.MapGet("service-types", GetAllServicetypes);

        return app;
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
            : await repositoryService.FindRepositoriesByTeam(team, excludeTemplates.GetValueOrDefault(),
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

    private static async Task<IResult> GetAllReposTemplatesLibraries(IRepositoryService repositoryService,
        ITemplatesService templatesService, string? team, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(team)) return Results.BadRequest(new { message = "The team must be specified" });

        var repositories = await repositoryService.FindRepositoriesByTeam(team, true, cancellationToken);

        var templates = await templatesService.FindTemplatesByTeam(team, cancellationToken);

        return Results.Ok(new AllRepoTemplatesLibrariesResponse(repositories, templates));
    }

    private static async Task<IResult> GetTemplateById(ITemplatesService templatesService, string templateId,
        CancellationToken cancellationToken)
    {
        var maybeTemplate = await templatesService.FindTemplateById(templateId, cancellationToken);
        return maybeTemplate == null
            ? Results.NotFound(new ApiError($"{templateId} not found"))
            : Results.Ok(new SingleTemplateResponse(maybeTemplate!));
    }

    private static async Task<IResult> GetAllTemplates(ITemplatesService templatesService, string? team,
        CancellationToken cancellationToken)
    {
        var templates = string.IsNullOrWhiteSpace(team)
            ? await templatesService.AllTemplates(cancellationToken)
            : await templatesService.FindTemplatesByTeam(team, cancellationToken);

        return Results.Ok(new MultipleTemplatesResponse(templates));
    }

    private static Task<IResult> GetAllServicetypes(ITemplatesService templatesService)
    {
        return Task.FromResult(Results.Ok(templatesService.AllServiceTypes()));
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

    public sealed record MultipleTemplatesResponse(string Message, IEnumerable<Repository> Templates)
    {
        public MultipleTemplatesResponse(IEnumerable<Repository> repositories) : this("success", repositories)
        {
        }
    }

    public sealed record SingleRepositoryResponse(string Message, Repository Repository)
    {
        public SingleRepositoryResponse(Repository repository) : this("success", repository)
        {
        }
    }

    public sealed record SingleTemplateResponse(string Message, Repository Template)
    {
        public SingleTemplateResponse(Repository repository) : this("success", repository)
        {
        }
    }
}