using Defra.Cdp.Backend.Api.Models;
using Octokit.GraphQL;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public sealed record RepositoryResult(string Name, string Description, string PrimaryLanguage, string Url,
    bool IsArchived, bool IsTemplate, bool IsPrivate, DateTimeOffset CreatedAt);

public sealed record TeamResult(string Slug,
    IEnumerable<RepositoryResult> Repositories);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories : IJob
{
    private readonly GithubCredentialAndConnectionFactory _githubCredentialAndConnectionFactory;
    private readonly ILogger<PopulateGithubRepositories> _logger;
    private readonly ICompiledQuery<IEnumerable<TeamResult>> _query;
    private readonly IRepositoryService _repositoryService;

    public PopulateGithubRepositories(IConfiguration configuration, ILoggerFactory loggerFactory,
        IRepositoryService repositoryService)
    {
        var githubOrgName = configuration.GetValue<string>("Github:Organisation")!;
        _githubCredentialAndConnectionFactory = new GithubCredentialAndConnectionFactory(configuration);

        _logger = loggerFactory.CreateLogger<PopulateGithubRepositories>();
        _repositoryService = repositoryService;

        // Compile the query once for efficiency
        _query = new Query()
            .Organization(githubOrgName)
            .Teams()
            .AllPages() //library does the automatic paging calls
            .Select(t =>
                new TeamResult(
                    t.Slug,
                    // optional arguments are not supported here in C# for some reason
                    // a limitation for anonymous types inside a LINQ query?
                    // `null` is the default for all optional parameters and have to be explicitly passed
                    t.Repositories(null, null, null, null, null, null)
                        .AllPages()
                        .Select(r => new
                        {
                            r.Name,
                            r.Description,
                            PrimaryLanguage = r.PrimaryLanguage.Name,
                            r.Url,
                            r.IsArchived,
                            r.IsTemplate,
                            r.IsPrivate,
                            r.CreatedAt
                        }).ToList().Select(r => new RepositoryResult(
                            r.Name, r.Description, r.PrimaryLanguage, r.Url, r.IsArchived, r.IsTemplate, r.IsPrivate,
                            r.CreatedAt
                        ))
                )).Compile();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Repopulating Github repositories");

        var githubConnection = await _githubCredentialAndConnectionFactory.GetGithubConnection();
        var queryRun = await githubConnection
            .Run(_query, cancellationToken: context.CancellationToken);
        var result = queryRun.ToList();


        var repositories = QueryResultToRepositories(result);

        await _repositoryService.UpsertMany(repositories, context.CancellationToken);
    }

    public static IEnumerable<Repository> QueryResultToRepositories(List<TeamResult> result)
    {
        var teamsAndReposPair =
            result
                .SelectMany(t =>
                    t.Repositories
                        .ToList()
                        .Select(r => new { Team = t.Slug, Repo = r.Name })).Distinct();
        var repoOwnerPair = teamsAndReposPair.GroupBy(
                pair => pair.Repo,
                pair => pair.Team,
                (repo, teams) => new { repo, teams })
            .ToDictionary(pair => pair.repo, pair => pair.teams);

        var repositories =
            result
                .SelectMany(t => t.Repositories.ToList())
                .DistinctBy(r => r.Name)
                .Select(r => new Repository
                {
                    Id = r.Name,
                    CreatedAt = r.CreatedAt,
                    Description = r.Description,
                    IsArchived = r.IsArchived,
                    IsPrivate = r.IsPrivate,
                    IsTemplate = r.IsTemplate,
                    Url = r.Url,
                    PrimaryLanguage = r.PrimaryLanguage,
                    Teams = (repoOwnerPair.GetValueOrDefault(r.Name) ?? Array.Empty<string>()).ToList()
                });
        return repositories;
    }
}