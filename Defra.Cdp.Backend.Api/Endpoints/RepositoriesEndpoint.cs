using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{
    private const string RepositoriesBaseRoute = "repositories";
    private const string RepositoriesTag = "Repositories";
    private const string GithubRepositoriesBaseRoute = "github-repo";

    private const string TemplatesBaseRoute = "templates";
    private const string TemplatesTag = "Templates";

    public static IEndpointRouteBuilder MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(RepositoriesBaseRoute,
                async (IRepositoryService repositoryService, [FromQuery(Name = "team")] string? team,
                        [FromQuery(Name = "excludeTemplates")] bool? excludeTemplates,
                        CancellationToken cancellationToken) =>
                    await GetAllRepositories(repositoryService, team, excludeTemplates, cancellationToken))
            .WithName("GetAllRepositories")
            .Produces<MultipleRepositoriesResponse>()
            .WithTags(RepositoriesTag);

        app.MapGet($"{RepositoriesBaseRoute}/services",
                async (IRepositoryService repositoryService, CancellationToken cancellationToken) =>
                    await GetRepositoriesByTopic(repositoryService, "service", cancellationToken))
            .WithName("GetAllRepositoriesWithServiceTopic")
            .Produces<MultipleRepositoriesResponse>()
            .WithTags(RepositoriesTag);

        app.MapGet($"{RepositoriesBaseRoute}/all/{{teamId}}", GetTeamRepositories)
            .WithName("GetTeamRepositories")
            .Produces<AllTeamRepositoriesResponse>()
            .WithTags(RepositoriesTag);

        app.MapGet($"{RepositoriesBaseRoute}/{{id}}", GetRepositoryById)
            .WithName("GetRepositoryById")
            .Produces<SingleRepositoryResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(RepositoriesTag);

        // ALL THE THINGS
        app.MapGet($"{GithubRepositoriesBaseRoute}/{{team}}", GetAllReposTemplatesLibraries)
            .WithName("GetRepositoryTemplatesLibrariesByTeam")
            .Produces<AllRepoTemplatesLibrariesResponse>()
            .WithTags(RepositoriesTag);

        // return as templates in the body
        app.MapGet(TemplatesBaseRoute,
                async (ITemplatesService templatesService, [FromQuery(Name = "team")] string? team,
                        CancellationToken cancellationToken) =>
                    await GetAllTemplates(templatesService, team, cancellationToken))
            .WithName("GetAllTemplates")
            .Produces<MultipleTemplatesResponse>()
            .WithTags(TemplatesTag);

        app.MapGet($"{TemplatesBaseRoute}/{{templateId}}", GetTemplateById)
            .WithName("GetTemplateById")
            .Produces<SingleTemplateResponse>()
            .Produces(StatusCodes.Status404NotFound) // should be 501 but we don't want to break frontend
            .WithName(TemplatesTag);

        app.MapGet("service-types", GetAllServicetypes)
            .WithName("GetAllServicetypes")
            .Produces<ServiceTypesResult>()
            .WithTags(TemplatesTag);

        return app;
    }

    private static async Task<IResult> GetRepositoryById(IRepositoryService repositoryService, string id,
        CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryById(id, cancellationToken);
        return maybeRepository == null
            ? Results.NotFound(new { message = $"{id} not found" })
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

    private static async Task<IResult> GetRepositoriesByTopic(IRepositoryService repositoryService, string topic,
        CancellationToken cancellationToken)
    {
        var repositories = await repositoryService.FindRepositoriesByTopic(topic, cancellationToken);

        return Results.Ok(new MultipleRepositoriesResponse(repositories));
    }

    private static async Task<IResult> GetTeamRepositories(IRepositoryService repositoryService, string teamId,
        CancellationToken cancellationToken)
    {
        var services = await repositoryService.FindTeamRepositoriesByTopic(teamId, "service",
            cancellationToken);

        var templates = await repositoryService.FindTeamRepositoriesByTopic(teamId, "template",
            cancellationToken);

        return Results.Ok(new AllTeamRepositoriesResponse(services, templates));
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
            ? Results.NotFound(new { message = $"{templateId} not found" })
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

    public sealed record AllTeamRepositoriesResponse(string Message, IEnumerable<Repository> Services,
        IEnumerable<Repository> Templates)
    {
        public AllTeamRepositoriesResponse(IEnumerable<Repository> services,
            IEnumerable<Repository> templates) : this("success", services, templates)
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