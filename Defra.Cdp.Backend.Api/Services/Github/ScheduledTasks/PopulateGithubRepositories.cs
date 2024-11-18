using System.Net.Http.Headers;
using System.Text;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Utils;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public sealed record RepositoryResult(
    string Name,
    RepositoryTopics Topics,
    string Description,
    string PrimaryLanguage,
    string Url,
    bool IsArchived,
    bool IsTemplate,
    bool IsPrivate,
    DateTimeOffset CreatedAt);

public sealed record TeamResult(
    string Slug,
    IEnumerable<RepositoryResult> Repositories);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories : IJob
{
    private const string LockName = "repopulateGithub";
    
    private readonly HttpClient _client; 

    private readonly string _githubOrgName;
    private readonly string _githubApiUrl;
    private readonly MongoLock _mongoLock;
    private readonly IGithubCredentialAndConnectionFactory _githubCredentialAndConnectionFactory;
    private readonly ILogger<PopulateGithubRepositories> _logger;
    private readonly IRepositoryService _repositoryService;

    private readonly UserServiceFetcher _userServiceFetcher;
    
    public PopulateGithubRepositories(IConfiguration configuration, ILoggerFactory loggerFactory,
        IRepositoryService repositoryService,
        MongoLock mongoLock,
        IHttpClientFactory clientFactory,
        IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory)
    {
        _client = clientFactory.CreateClient(Proxy.ProxyClient);
        _githubOrgName = configuration.GetValue<string>("Github:Organisation")!;
        _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}/graphql";

        _mongoLock = mongoLock;
        _githubCredentialAndConnectionFactory = githubCredentialAndConnectionFactory;

        _logger = loggerFactory.CreateLogger<PopulateGithubRepositories>();
        _repositoryService = repositoryService;
        _userServiceFetcher = new UserServiceFetcher(configuration);

        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "cdp-portal-backend");
    }

    public async Task Execute(IJobExecutionContext context)
    {
            if (await _mongoLock.Lock(LockName, TimeSpan.FromSeconds(60)))
            {
                try
                {
                    await RepopulateGithubRepos(context);
                }
                catch (Exception e)
                {
                    _logger.LogError("RepopulateGithub scheduled job failed: {e}", e);
                }
                finally
                {
                    await _mongoLock.Unlock(LockName);
                }
            }
    }

    private async Task RepopulateGithubRepos(IJobExecutionContext context)
    {
        _logger.LogInformation("Repopulating Github repositories");
        var cancellationToken = context.CancellationToken;

        var userServiceRecords = await _userServiceFetcher.GetLatestCdpTeamsInformation(cancellationToken);
        var githubToTeamIdMap = userServiceRecords?.GithubToTeamIdMap ?? new Dictionary<string, string>();
        var githubToTeamNameMap = userServiceRecords?.GithubToTeamNameMap ?? new Dictionary<string, string>();
        
        var token = await _githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        
        
        List<Repository> repositories = new(); 
        
        // paginate
        var hasNext = true;
        string? nextCursor = null;
        while (hasNext)
        {
            _logger.LogInformation("Retrieving paginated GitHub team data.");
            var query = BuildTeamsQuery(_githubOrgName, nextCursor);
            var jsonResponse = await _client.PostAsync(
                _githubApiUrl,
                new StringContent(query, Encoding.UTF8, "application/json"),
                cancellationToken
            );
            jsonResponse.EnsureSuccessStatusCode();
        
            var result = await jsonResponse.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: cancellationToken);
            if (result is null)
            {
                var jsonString = jsonResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("The following was invalid json: {@JsonString}", jsonString);
                throw new ApplicationException("response must be parsed correct");
            }

            var repos = QueryResultToRepositories(result, githubToTeamIdMap, githubToTeamNameMap).ToList();
            repositories.AddRange(repos);
            _logger.LogInformation("Added {repos} repos, total {total}", repos.Count, repositories.Count);
            hasNext = result.data.organization.teams.pageInfo.hasNextPage;
            nextCursor = result.data.organization.teams.pageInfo.endCursor;
           
        }
        
        await _repositoryService.UpsertMany(repositories, cancellationToken);
        await _repositoryService.DeleteUnknownRepos(repositories.Select(r => r.Id), cancellationToken);
        _logger.LogInformation("Successfully repopulated repositories and team information");
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
                        .Where(t => !string.IsNullOrEmpty(t.TeamId))
                    ,
                    Topics = r.Topics.nodes.Select(t => t.topic.name)
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
                                githubToTeamNameMap.GetValueOrDefault(t))),
                        Topics = r.repositoryTopics.nodes.Select(t => t.topic.name)
                    };
                });
        return repositories;
    }

    // Request based on this GraphQL query.
    // To test it, you can login to Github and try it here: https://docs.github.com/en/graphql/overview/explorer
    // 'query { organization(login: "Defra") { id teams(first: 100) { pageInfo { hasNextPage endCursor } nodes { slug repositories { nodes { name repositoryTopics(first: 30) { nodes { topic { name }}} description primaryLanguage { name } url isArchived isTemplate isPrivate createdAt } } } } }}'
    private static string BuildTeamsQuery(string githubOrgName, string? cursor)
    {
        var afterCursor =  "null";
        if (cursor != null)
        {
            afterCursor = "\\\"" + cursor + "\\\"";
        } 
        
        return $@"
            {{
              ""query"": 
                 ""query {{ organization(login: \""{githubOrgName}\"") {{ id teams(first: 100, after: {afterCursor}) {{ pageInfo {{ hasNextPage endCursor }} nodes {{ slug repositories {{ nodes {{ name repositoryTopics(first: 30) {{ nodes {{ topic {{ name }} }} }} description primaryLanguage {{ name }} url isArchived isTemplate isPrivate createdAt }} }} }} }} }}}}""
            }}";
    }
}