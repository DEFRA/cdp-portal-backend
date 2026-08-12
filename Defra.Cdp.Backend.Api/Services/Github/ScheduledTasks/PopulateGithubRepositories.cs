using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    IRepositoryService repositoryService,
    IMongoLock mongoLock,
    IHttpClientFactory clientFactory,
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    HeaderPropagationValues headerPropagationValues)
    : IJob
{
    private const string LockName = "repopulateGithub";

    private readonly HttpClient _client = clientFactory.CreateClient("GitHubClient");
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}/graphql";
    private readonly string _githubOrgName = configuration.GetValue<string>("Github:Organisation")!;

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

        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var githubRepositoryNodes = await GetReposFromGithub(cancellationToken);
        var repositories = BuildRepositories(githubRepositoryNodes);

        await repositoryService.UpsertMany(repositories, cancellationToken);
        await repositoryService.DeleteUnknownRepos(repositories.Select(r => r.Id), cancellationToken);
        _logger.LogInformation("Successfully repopulated repositories");
    }

    public static List<Repository> BuildRepositories(IEnumerable<RepositoryNode> githubRepositoryNodes)
    {
        return githubRepositoryNodes
            .Select(repo =>
            {
                return new Repository
                {
                    Id = repo.name,
                    CreatedAt = repo.createdAt,
                    Description = repo.description,
                    IsArchived = repo.isArchived,
                    Url = repo.url,
                    PrimaryLanguage = repo.primaryLanguage?.name ?? "Unknown",
                    Topics = repo.repositoryTopics.nodes.Select(t => t.topic.name)
                };
            })
            .ToList();
    }

    public static List<RepositoryNode> RemoveNullRepositoryNodes(IEnumerable<RepositoryNode?> githubRepositoryNodes)
    {
        return githubRepositoryNodes.Where(node => node is not null).Select(node => node!).ToList();
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