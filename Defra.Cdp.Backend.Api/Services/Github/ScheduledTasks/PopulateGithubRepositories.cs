using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Audit;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PopulateGithubRepositories(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    IRepositoryService repositoryService,
    MongoLock mongoLock,
    IHttpClientFactory clientFactory,
    IUserServiceFetcher userServiceFetcher,
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    HeaderPropagationValues headerPropagationValues,
    IEntitiesService entitiesService,
    IEntityStatusService entityStatusService,
    ITenantServicesService tenantService)
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
        {
            try
            {
                // Workaround mentioned in https://github.com/alefranz/HeaderPropagation/issues/5
                headerPropagationValues.Headers ??=
                    new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                await RepopulateGithubRepos(context);

                var repos = await repositoryService.AllRepositories(true, context.CancellationToken);
                await entitiesService.RefreshTeams(repos, context.CancellationToken);
                await tenantService.RefreshTeams(repos, context.CancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError("RepopulateGithub scheduled job failed: {e}", e);
            }
            finally
            {
                await mongoLock.Unlock(LockName);
                await entityStatusService.UpdatePendingEntityStatuses(context.CancellationToken);
            }
        }
    }

    private async Task RepopulateGithubRepos(IJobExecutionContext context)
    {
        _logger.LogInformation("Repopulating Github repositories");
        var cancellationToken = context.CancellationToken;

        var cdpTeams = await userServiceFetcher.GetLatestCdpTeamsInformation(cancellationToken);

        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        if (token is null) throw new ArgumentNullException("token", "Installation token cannot be null");
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var repositoryNodesByTeam = await GetReposFromGithubByTeam(cancellationToken, cdpTeams?.teams ?? []);

        var repositories = GroupRepositoriesByTeam(repositoryNodesByTeam, _logger);

        await repositoryService.UpsertMany(repositories, cancellationToken);
        await repositoryService.DeleteUnknownRepos(repositories.Select(r => r.Id), cancellationToken);
        _logger.LogInformation("Successfully repopulated repositories and team information");
    }

    public static List<Repository> GroupRepositoriesByTeam(
        Dictionary<UserServiceTeam, List<RepositoryNode>> repositoryNodesByTeam, ILogger<PopulateGithubRepositories> logger)
    {
        var repoMap = new Dictionary<string, Repository>();

        foreach (var (userServiceTeam, repos) in repositoryNodesByTeam)
        {
            if (userServiceTeam.github is null)
            {
                logger.LogWarning("Skipping team with no GitHub slug: {@UserServiceTeam}", userServiceTeam);
                continue;
            }
            
            var team = new RepositoryTeam
            (
                userServiceTeam.github, 
                userServiceTeam.teamId,
                userServiceTeam.name
            );
            
            foreach (var repo in repos)
            {
                if (!repoMap.TryGetValue(repo.name, out var existingRepo))
                {
                    existingRepo = new Repository
                    {
                        Id = repo.name,
                        CreatedAt = repo.createdAt,
                        Description = repo.description,
                        IsArchived = repo.isArchived,
                        IsPrivate = repo.isPrivate,
                        IsTemplate = repo.isTemplate,
                        Url = repo.url,
                        PrimaryLanguage = repo.primaryLanguage?.name ?? "Unknown",
                        Teams = [],
                        Topics = repo.repositoryTopics.nodes.Select(t => t.topic.name)
                    };
                    ;
                    repoMap[repo.name] = existingRepo;
                }

                if (!existingRepo.Teams.Contains(team))
                {
                    existingRepo.Teams.Add(team);
                }
            }
        }

        return repoMap.Values.ToList();
    }

    private async Task<Dictionary<UserServiceTeam, List<RepositoryNode>>> GetReposFromGithubByTeam(
        CancellationToken cancellationToken,
        List<UserServiceTeam> cdpTeams)
    {
        var repositoriesByTeam = new Dictionary<UserServiceTeam, List<RepositoryNode>>();

        foreach (var team in cdpTeams)
        {
            var teamSlug = team.github;
            if (string.IsNullOrEmpty(teamSlug)) continue;

            _logger.LogInformation("Processing team: {TeamSlug}", teamSlug);

            string? repoCursor = null;
            var hasMoreRepos = true;
            while (hasMoreRepos)
            {
                var reposQuery = BuildReposQuery(_githubOrgName, teamSlug, repoCursor);
                var jsonResponseRepos = await _client.PostAsync(
                    _githubApiUrl,
                    reposQuery,
                    cancellationToken
                );
                jsonResponseRepos.EnsureSuccessStatusCode();

                var result = await jsonResponseRepos.Content.ReadFromJsonAsync<RepoQueryResponse>(cancellationToken);
                if (result is null)
                {
                    var jsonString = jsonResponseRepos.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("The following was invalid json: {@JsonString}", jsonString);
                    throw new ApplicationException("response must be parsed correct");
                }

                if (result.data == null)
                {
                    continue;
                }

                var repos = (result
                    .data
                    .organization
                    .team
                    ?.repositories.nodes ?? []
                    ).ToList() ;

                if (!repositoriesByTeam.TryAdd(team, repos))
                {
                    repositoriesByTeam[team].AddRange(repos);
                }

                _logger.LogInformation("Added {repos} repos, total {total}", repos.Count,
                    repositoriesByTeam[team].Count);

                hasMoreRepos = result.data.organization.team?.repositories.pageInfo.hasNextPage ?? false;
                repoCursor = result.data.organization.team?.repositories.pageInfo.endCursor ?? null;
            }
        }

        return repositoriesByTeam;
    }

    // To test queries, you can login to Github and try it here: https://docs.github.com/en/graphql/overview/explorer
    private static StringContent BuildReposQuery(string githubOrgName, string teamSlug, string? repoCursor)
    {
        var reposQuery = new
        {
            query = @"
                        query ($githubOrgName: String!, $teamSlug: String!, $repoCursor: String) {
                          organization(login: $githubOrgName) {
                            team(slug: $teamSlug) {
                              repositories(first: 100, after: $repoCursor) {
                                pageInfo {
                                  hasNextPage
                                  endCursor
                                }
                                nodes {
                                  name,
                                  repositoryTopics(first: 30) {
                                    nodes {
                                      topic {
                                        name
                                      }
                                    }
                                  },
                                  description,
                                  primaryLanguage {
                                    name
                                  },
                                  url,
                                  isArchived,
                                  isTemplate,
                                  isPrivate,
                                  createdAt
                                }
                              }
                            }
                          }
                        }
                    ",
            variables = new { githubOrgName, teamSlug, repoCursor }
        };

        return new StringContent(JsonSerializer.Serialize(reposQuery), Encoding.UTF8, "application/json");
    }
}