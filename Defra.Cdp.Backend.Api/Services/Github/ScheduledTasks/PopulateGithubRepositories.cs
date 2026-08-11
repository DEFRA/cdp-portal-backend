using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Teams;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    IRepositoryService repositoryService,
    IEntitiesService entitiesService,
    IMongoLock mongoLock,
    IHttpClientFactory clientFactory,
    ITeamsService teamsService,
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    HeaderPropagationValues headerPropagationValues)
    : IJob
{
    private const string LockName = "repopulateGithub";

    private readonly HttpClient _client = clientFactory.CreateClient("GitHubClient");
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}/graphql";
    private readonly string _githubOrgName = configuration.GetValue<string>("Github:Organisation")!;
    private readonly string _githubRestApiUrl = configuration.GetValue<string>("Github:ApiUrl")!;

    private readonly ILogger<PopulateGithubRepositories> _logger =
        loggerFactory.CreateLogger<PopulateGithubRepositories>();

    public async Task Execute(IJobExecutionContext context)
    {
        if (await mongoLock.Lock(LockName, TimeSpan.FromSeconds(60)))
            try
            {
                // Workaround mentioned in https://github.com/alefranz/HeaderPropagation/issues/5
                headerPropagationValues.Headers ??=
                    new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                await RepopulateGithubRepos(context);
            }
            catch (Exception e)
            {
                _logger.LogError("RepopulateGithub scheduled job failed: {e}", e);
            }
            finally
            {
                await mongoLock.Unlock(LockName);
            }
    }

    private async Task RepopulateGithubRepos(IJobExecutionContext context)
    {
        _logger.LogInformation("Repopulating Github repositories");
        var cancellationToken = context.CancellationToken;

        var cdpTeamsByGithubSlug = await GetRepositoryTeams(cancellationToken);
        var entityNames = await GetEntityNames(cancellationToken);

        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var githubRepositoryNodes = await GetReposFromGithub(cancellationToken);
        var githubTeamsByRepoName = await GetGithubTeamsByRepoName(
            githubRepositoryNodes,
            entityNames,
            cdpTeamsByGithubSlug,
            cancellationToken
        );

        var repositories = BuildRepositories(githubRepositoryNodes, entityNames, githubTeamsByRepoName);

        await repositoryService.UpsertMany(repositories, cancellationToken);
        await repositoryService.DeleteUnknownRepos(repositories.Select(r => r.Id), cancellationToken);
        _logger.LogInformation("Successfully repopulated repositories and team information");
    }

    public static List<Repository> BuildRepositories(
        IEnumerable<RepositoryNode> githubRepositoryNodes,
        HashSet<string> entityNames,
        Dictionary<string, List<RepositoryTeam>> githubTeamsByRepoName)
    {
        return githubRepositoryNodes
            .Select(repo =>
            {
                var teams = entityNames.Contains(repo.name)
                    ? [] // Entity is the source of truth for services/test suites; skip storing GitHub ownership on the repo
                    : githubTeamsByRepoName.GetValueOrDefault(repo.name) ?? [];

                return new Repository
                {
                    Id = repo.name,
                    CreatedAt = repo.createdAt,
                    Description = repo.description,
                    IsArchived = repo.isArchived,
                    Url = repo.url,
                    PrimaryLanguage = repo.primaryLanguage?.name ?? "Unknown",
                    Teams = teams,
                    Topics = repo.repositoryTopics.nodes.Select(t => t.topic.name)
                };
            })
            .ToList();
    }

    public static List<RepositoryNode> RemoveNullRepositoryNodes(IEnumerable<RepositoryNode?> githubRepositoryNodes)
    {
        return githubRepositoryNodes.Where(node => node is not null).Select(node => node!).ToList();
    }

    private async Task<HashSet<string>> GetEntityNames(CancellationToken cancellationToken)
    {
        var entities = await entitiesService.GetEntityIds(new EntityMatcher(), cancellationToken);
        return entities.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // Entity-backed services/test-suites already get ownership from Entity.Teams, so we skip
    // GitHub team lookup for them. For repos with no Entity (templates, libraries, and other
    // CDP-tagged repos), GitHub remains the only ownership source — fetch teams for those only.
    private async Task<Dictionary<string, List<RepositoryTeam>>> GetGithubTeamsByRepoName(
        IEnumerable<RepositoryNode> githubRepositoryNodes,
        HashSet<string> entityNames,
        Dictionary<string, RepositoryTeam> cdpTeamsByGithubSlug,
        CancellationToken cancellationToken)
    {
        var githubTeamsByRepoName = new Dictionary<string, List<RepositoryTeam>>(StringComparer.OrdinalIgnoreCase);
        var githubRepos = githubRepositoryNodes.ToList();
        var repositoriesWithoutEntity = githubRepos
            .Where(repo => !entityNames.Contains(repo.name))
            .ToList();
        var repositoriesWithEntity = githubRepos.Count - repositoriesWithoutEntity.Count;

        _logger.LogInformation(
            "Fetching GitHub teams for {RestTeamLookupCount} repos without an Entity ({EntityBackedCount} entity-backed)",
            repositoriesWithoutEntity.Count,
            repositoriesWithEntity);

        foreach (var githubRepositoryNode in repositoriesWithoutEntity)
        {
            var teams = await GetTeamsForRepo(githubRepositoryNode.name, cdpTeamsByGithubSlug, cancellationToken);
            githubTeamsByRepoName[githubRepositoryNode.name] = teams;
        }

        return githubTeamsByRepoName;
    }

    private async Task<Dictionary<string, RepositoryTeam>> GetRepositoryTeams(CancellationToken cancellationToken)
    {
        var cdpTeams = await teamsService.FindAll(cancellationToken);

        return cdpTeams
            .Where(t =>
            {
                if (t.Github is not null) return true;

                _logger.LogWarning("Skipping team with no GitHub slug: {@UserServiceTeam}", t);
                return false;
            })
            .GroupBy(t => t.Github!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var team = group.First();
                    return new RepositoryTeam(team.Github!, team.TeamId, team.TeamName);
                },
                StringComparer.OrdinalIgnoreCase
            );
    }

    private async Task<List<RepositoryNode>> GetReposFromGithub(CancellationToken cancellationToken)
    {
        var githubRepositoryNodes = new List<RepositoryNode>();
        string? repoCursor = null;
        bool hasMoreRepos;

        do
        {
            var page = await FetchSearchRepositoriesPage(repoCursor, cancellationToken);
            if (page is null) break;

            var repos = RemoveNullRepositoryNodes(page.Nodes);
            githubRepositoryNodes.AddRange(repos);

            _logger.LogInformation("Added {repos} repos, total {total}", repos.Count, githubRepositoryNodes.Count);

            hasMoreRepos = page.HasNextPage;
            repoCursor = page.EndCursor;
        } while (hasMoreRepos);

        return githubRepositoryNodes;
    }

    private async Task<SearchRepositoriesPage?> FetchSearchRepositoriesPage(
        string? repoCursor,
        CancellationToken cancellationToken)
    {
        var reposQuery = BuildSearchReposQuery($"org:{_githubOrgName} topic:cdp", repoCursor);
        var jsonResponseRepos = await _client.PostAsync(_githubApiUrl, reposQuery, cancellationToken);
        jsonResponseRepos.EnsureSuccessStatusCode();

        var result = await jsonResponseRepos.Content.ReadFromJsonAsync<SearchRepoQueryResponse>(cancellationToken);
        if (result is null)
        {
            var jsonString = await jsonResponseRepos.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("The following was invalid json: {@JsonString}", jsonString);
            throw new ApplicationException("response must be parsed correct");
        }

        if (result.data is null) return null;

        return new SearchRepositoriesPage(
            result.data.search.nodes,
            result.data.search.pageInfo.hasNextPage,
            result.data.search.pageInfo.endCursor
        );
    }

    private sealed record SearchRepositoriesPage(
        IEnumerable<RepositoryNode?> Nodes,
        bool HasNextPage,
        string? EndCursor
    );

    // Some CDP-tagged repos predate the GitHub App's installation, so GitHub returns 403 for
    // those specifically. Treat only 403 as "no teams known".
    private async Task<List<RepositoryTeam>> GetTeamsForRepo(
        string repoName,
        Dictionary<string, RepositoryTeam> cdpTeamsByGithubSlug,
        CancellationToken cancellationToken)
    {
        var teamsUrl = $"{_githubRestApiUrl}/repos/{_githubOrgName}/{repoName}/teams";
        var response = await _client.GetAsync(teamsUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Forbidden fetching GitHub teams for repo {RepoName} (app likely lacks access to this repo). Treating as no teams.",
                repoName);
            return [];
        }

        response.EnsureSuccessStatusCode();

        var githubTeams = await response.Content.ReadFromJsonAsync<List<GithubTeamSummary>>(cancellationToken) ?? [];
        return githubTeams
            .Where(team => cdpTeamsByGithubSlug.ContainsKey(team.slug))
            .Select(team => cdpTeamsByGithubSlug[team.slug])
            .Distinct()
            .ToList();
    }

    // To test queries, you can use gh api graphql -f query='query { viewer { login } }'. See: https://docs.github.com/en/graphql/guides/using-graphql-clients
    private static StringContent BuildSearchReposQuery(string searchQuery, string? repoCursor)
    {
        var reposQuery = new
        {
            query = @"
                        query ($searchQuery: String!, $repoCursor: String) {
                          search(query: $searchQuery, type: REPOSITORY, first: 100, after: $repoCursor) {
                            pageInfo {
                              hasNextPage
                              endCursor
                            }
                            nodes {
                              ... on Repository {
                                name
                                repositoryTopics(first: 30) {
                                  nodes {
                                    topic {
                                      name
                                    }
                                  }
                                }
                                description
                                primaryLanguage {
                                  name
                                }
                                url
                                isArchived
                                createdAt
                              }
                            }
                          }
                        }
                    ",
            variables = new { searchQuery, repoCursor }
        };

        return new StringContent(JsonSerializer.Serialize(reposQuery), Encoding.UTF8, "application/json");
    }
}