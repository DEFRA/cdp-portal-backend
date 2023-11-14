using System.Net.Http.Headers;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public sealed record RepositoryResult(string Name, string Description, string PrimaryLanguage, string Url,
    bool IsArchived, bool IsTemplate, bool IsPrivate, DateTimeOffset CreatedAt);

public sealed record TeamResult(string Slug,
    IEnumerable<RepositoryResult> Repositories);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories : IJob
{
    private readonly HttpClient _client = new();
    private readonly IDeployablesService _deployablesService;
    private readonly GithubCredentialAndConnectionFactory _githubCredentialAndConnectionFactory;
    private readonly ILogger<PopulateGithubRepositories> _logger;
    private readonly IRepositoryService _repositoryService;

    private readonly string _requestString;
    private readonly UserServiceFetcher _userServiceFetcher;
    private bool _canUpdateDeployableArtifacts = true;


    public PopulateGithubRepositories(IConfiguration configuration, ILoggerFactory loggerFactory,
        IRepositoryService repositoryService, IDeployablesService deployablesService)
    {
        var githubOrgName = configuration.GetValue<string>("Github:Organisation")!;
        _githubCredentialAndConnectionFactory = new GithubCredentialAndConnectionFactory(configuration);

        _logger = loggerFactory.CreateLogger<PopulateGithubRepositories>();
        _repositoryService = repositoryService;
        _deployablesService = deployablesService;

        _requestString =
            $@"
            {{
              ""query"": 
                 ""query {{ organization(login: \""{githubOrgName}\"") {{ id teams(first: 100) {{ pageInfo {{ hasNextPage endCursor }} nodes {{ slug repositories {{ nodes {{ name description primaryLanguage {{ name }} url isArchived isTemplate isPrivate createdAt }} }} }} }} }}}}""
            }}";

        _userServiceFetcher = new UserServiceFetcher(configuration);


        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "cdp-portal-backend");
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Repopulating Github repositories");

        var token = await _githubCredentialAndConnectionFactory.GetToken();
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var jsonResponse = await _client.PostAsync(
            "https://api.github.com/graphql",
            new StringContent(_requestString),
            context.CancellationToken
        );
        var userServiceRecords = _userServiceFetcher.getLatestCdpTeamsInformation();
        var githubToTeamIdMap = userServiceRecords.Result?.GithubToTeamIdMap ?? new Dictionary<string, string>();
        var githubToTeamNameMap = userServiceRecords.Result?.GithubToTeamNameMap ?? new Dictionary<string, string>();
        var result = await jsonResponse.Content.ReadFromJsonAsync<QueryResponse>();
        if (result is null)
        {
            var jsonString = jsonResponse.Content.ReadAsStringAsync();
            _logger.LogError("The following was invalid json: {@JsonString}", jsonString);
            throw new ApplicationException("response must be parsed correct");
        }

        var repositories = QueryResultToRepositories(result, githubToTeamIdMap, githubToTeamNameMap).ToList();

        await _repositoryService.UpsertMany(repositories, context.CancellationToken);
        await _repositoryService.DeleteUnknownRepos(repositories.Select(r => r.Id), context.CancellationToken);
        if (_canUpdateDeployableArtifacts)
        {
            await _deployablesService.UpdateAll(repositories, context.CancellationToken);
            _canUpdateDeployableArtifacts = false;
        }
    }

    public static IEnumerable<Repository> QueryResultToRepositories(List<TeamResult> result,
        Dictionary<string, string> githubToTeamIdMap, Dictionary<string, string> githubToTeamNameMap)
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
                        .Select(t => new RepositoryTeam(t, githubToTeamIdMap.GetValueOrDefault(t),
                            githubToTeamNameMap.GetValueOrDefault(t)))
                });
        return repositories;
    }

    public static IEnumerable<Repository> QueryResultToRepositories(QueryResponse result,
        Dictionary<string, string> githubToTeamIdMap, Dictionary<string, string> githubToTeamNameMap)
    {
        var teamsAndReposPair =
            result.data.organization.teams.nodes
                .SelectMany(t =>
                    t.repositories.nodes
                        .Select(r => new { Team = t.slug, Repo = r.name })).Distinct();
        var repoOwnerPair = teamsAndReposPair.GroupBy(
                pair => pair.Repo,
                pair => pair.Team,
                (repo, teams) => new { repo, teams })
            .ToDictionary(pair => pair.repo, pair => pair.teams);

        var repositories =
            result
                .data
                .organization
                .teams
                .nodes
                .SelectMany(t => t.repositories.nodes)
                .DistinctBy(r => r.name)
                .Select(r =>
                {
                    var primaryLanguage = r.primaryLanguage?.name ?? "none";
                    return new Repository
                    {
                        Id = r.name,
                        CreatedAt = r.createdAt,
                        Description = r.description,
                        IsArchived = r.isArchived,
                        IsPrivate = r.isPrivate,
                        IsTemplate = r.isTemplate,
                        Url = r.url,
                        PrimaryLanguage = primaryLanguage,
                        Teams = (repoOwnerPair.GetValueOrDefault(r.name) ?? Array.Empty<string>()).ToList()
                            .Select(t => new RepositoryTeam(t, githubToTeamIdMap.GetValueOrDefault(t),
                                githubToTeamNameMap.GetValueOrDefault(t)))
                    };
                });
        return repositories;
    }
}