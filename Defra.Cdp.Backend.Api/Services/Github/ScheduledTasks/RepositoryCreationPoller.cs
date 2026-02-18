using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Teams;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;
using Quartz;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RepositoryCreationPoller(
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
    private const string LockName = "processCreatingStatusEntities";

    private readonly HttpClient _client = clientFactory.CreateClient("GitHubClient");
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}/graphql";


    private readonly string _githubOrgName = configuration.GetValue<string>("Github:Organisation")!;

    private readonly ILogger<RepositoryCreationPoller> _logger =
        loggerFactory.CreateLogger<RepositoryCreationPoller>();

    public async Task Execute(IJobExecutionContext context)
    {
        if (await mongoLock.Lock(LockName, TimeSpan.FromSeconds(60)))
        {
            try
            {
                var cancellationToken = context.CancellationToken;

                var entities = (await entitiesService.GetCreatingEntities(cancellationToken))
                    .Where(e => e.Created?.Date == DateTime.Today)
                    .ToList();

                if (entities.Count != 0)
                {
                    _logger.LogInformation("Attempting to add repositories: {repos}", string.Join(", ", entities.Select(x => x.Name)));
                    // Workaround mentioned in https://github.com/alefranz/HeaderPropagation/issues/5
                    headerPropagationValues.Headers ??=
                        new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                    var githubRepos = await GetGithubRepos(entities.Select(e => e.Name).ToList(), cancellationToken);
                    var repositoryTeams = await GetRepositoryTeams(cancellationToken);
                    var repositories = BuildRepositories(entities, githubRepos, repositoryTeams);

                    await repositoryService.UpsertMany(repositories, cancellationToken);
                    if (repositories.Count != 0)
                    {
                        _logger.LogInformation("Successfully upserted {count} repositories", repositories.Count);
                    }
                    

                    var repositoryTypeEntities = entities
                        .Where(e =>
                            e.Type == Type.Repository &&
                            githubRepos.ContainsKey(e.Name))
                        .ToList();

                    await entitiesService.UpdateRepositoryStatusToCreated(repositoryTypeEntities, cancellationToken);
                }
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
    }

    private async Task<Dictionary<string, RepositoryTeam>> GetRepositoryTeams(CancellationToken cancellationToken)
    {
        var cdpTeams = await teamsService.FindAll(cancellationToken);

        return cdpTeams
            .Where(t =>
            {
                if (t.Github is not null)
                {
                    return true;
                }

                _logger.LogWarning("Skipping team with no GitHub slug: {@UserServiceTeam}", t);
                return false;
            })
            .ToDictionary(
                t => t.TeamId,
                t => new RepositoryTeam(t.Github!, t.TeamId, t.TeamName)
            );
    }

    private static List<Repository> BuildRepositories(
        List<Entity> entities,
        Dictionary<string, RepositoryNode>? repositoryNodes,
        Dictionary<string, RepositoryTeam> repositoryTeams)
    {
        return entities
            .Select(e =>
            {
                if (repositoryNodes is null || !repositoryNodes.TryGetValue(e.Name, out var repo)) return null;
                var teams = e.Teams?
                    .Where(t => t.TeamId is not null &&
                                repositoryTeams.TryGetValue(t.TeamId, out _))
                    .Select(t => repositoryTeams[t.TeamId!])
                    .Distinct()
                    .ToList() ?? [];

                return new Repository
                {
                    Id = repo!.name,
                    CreatedAt = repo.createdAt,
                    Description = repo.description,
                    IsArchived = repo.isArchived,
                    IsPrivate = repo.isPrivate,
                    IsTemplate = repo.isTemplate,
                    Url = repo.url,
                    PrimaryLanguage = repo.primaryLanguage?.name ?? "Unknown",
                    Teams = teams,
                    Topics = repo.repositoryTopics.nodes.Select(t => t.topic.name)
                };
            })
            .OfType<Repository>()
            .ToList();
    }


    private async Task<Dictionary<string, RepositoryNode>> GetGithubRepos(
        List<string> repos, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching new creating repositories");
        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var reposQuery = BuildRepoQuery(repos);
        var jsonResponseRepos = await _client.PostAsync(
            _githubApiUrl,
            reposQuery,
            cancellationToken
        );
        jsonResponseRepos.EnsureSuccessStatusCode();

        var result = await jsonResponseRepos.Content.ReadFromJsonAsync<RepoAliasQueryResponse>(cancellationToken);
        if (result is not null)
        {
            return result
                .data
                .Values
                .OfType<RepositoryNode>()
                .ToDictionary(r => r.name, r => r);
        }

        var jsonString = await jsonResponseRepos.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("The following was invalid json: {@JsonString}", jsonString);
        throw new ApplicationException("response must be parsed correct");
    }

    private StringContent BuildRepoQuery(IEnumerable<string> repoNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("query {");

        var i = 0;
        foreach (var repoName in repoNames)
        {
            sb.AppendLine($@"
      repo_{i++}: repository(owner: ""{_githubOrgName}"", name: ""{repoName}"") {{
        name
        description
        url
        isArchived
        isTemplate
        isPrivate
        createdAt
        primaryLanguage {{
          name
        }}
        repositoryTopics(first: 30) {{
          nodes {{
            topic {{
              name
            }}
          }}
        }}
      }}");
        }

        sb.AppendLine("}");

        return new StringContent(
            JsonSerializer.Serialize(new { query = sb.ToString() }),
            Encoding.UTF8,
            "application/json"
        );
    }

    private record RepoAliasQueryResponse(
        Dictionary<string, RepositoryNode?> data
    );
}