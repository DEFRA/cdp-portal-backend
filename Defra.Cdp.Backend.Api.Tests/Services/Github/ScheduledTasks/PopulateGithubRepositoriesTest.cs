using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class PopulateGithubRepositoriesTest
{
    // GitHub's GraphQL API can return null entries in paginated `nodes` arrays.
    // We now filter these before building repositories.
    [Fact]
    public void RemoveNullRepositoryNodesFiltersNullEntries()
    {
        var dateTimeNow = DateTimeOffset.Now;
        var nodes = new List<RepositoryNode?>
        {
            null,
            new RepositoryNode("repo1", Topics.CreateMockTopics(), "desc1", null, "url1", false, dateTimeNow)
        };

        var githubRepositoryNodes = PopulateGithubRepositories.RemoveNullRepositoryNodes(nodes);

        Assert.Single(githubRepositoryNodes);
        Assert.Equal("repo1", githubRepositoryNodes[0].name);
    }

    [Fact]
    public void JsonDeserializationProducesNullListEntryForNullNode()
    {
        const string json = """
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": "abc" },
              "nodes": [
                null,
                { "name": "repo1", "repositoryTopics": { "nodes": [] }, "description": "d", "primaryLanguage": null, "url": "u", "isArchived": false, "createdAt": "2024-01-01T00:00:00Z" }
              ]
            }
          }
        }
        """;

        var result = JsonSerializer.Deserialize<SearchRepoQueryResponse>(json);
        var nodes = result!.data!.search.nodes.ToList();

        Assert.Equal(2, nodes.Count);
        Assert.Null(nodes[0]);
        Assert.NotNull(nodes[1]);
        Assert.Equal("repo1", nodes[1]!.name);
    }

    [Fact]
    public void BuildRepositoriesUsesEntityTeamsForEntityBackedRepos()
    {
        var topics = Topics.CreateMockTopics();
        var dateTimeNow = DateTimeOffset.Now;
        var repositories = PopulateGithubRepositories.BuildRepositories(
            [
                new RepositoryNode("repo1", topics, "desc1", new PrimaryLanguage("Javascript"),
                    "https://url1", false, dateTimeNow),
                new RepositoryNode("repo2", topics, "desc2", new PrimaryLanguage("C#"),
                    "https://url2", false, dateTimeNow)
            ],
            new HashSet<string>(["repo1"], StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<RepositoryTeam>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "repo1",
                    [
                        new RepositoryTeam("cdp-platform", "platform-team-id", "Platform")
                    ]
                },
                {
                    "repo2",
                    [
                        new RepositoryTeam("fisheries", "fisheries-team-id", "Fisheries")
                    ]
                }
            });

        var topicNames = topics.nodes.Select(t => t.topic.name).ToList();
        var expected = new List<Repository>
        {
            new()
            {
                Id = "repo1",
                Topics = topicNames,
                CreatedAt = dateTimeNow,
                Description = "desc1",
                IsArchived = false,
                PrimaryLanguage = "Javascript",
                Url = "https://url1",
                Teams = []
            },
            new()
            {
                Id = "repo2",
                Topics = topicNames,
                CreatedAt = dateTimeNow,
                Description = "desc2",
                IsArchived = false,
                PrimaryLanguage = "C#",
                Url = "https://url2",
                Teams = [new RepositoryTeam("fisheries", "fisheries-team-id", "Fisheries")]
            }
        };
        
        Assert.Equivalent(expected, repositories);
    }

    [Fact]
    public void BuildRepositoriesDoesNotFailWhenNodesContainNullEntries()
    {
        var dateTimeNow = DateTimeOffset.Now;
        var topics = Topics.CreateMockTopics();
        var repositoryNodes = new List<RepositoryNode?>
        {
            null,
            new RepositoryNode(
                "repo1",
                topics,
                "desc1",
                new PrimaryLanguage("Javascript"),
                "https://url1",
                false,
                dateTimeNow
            )
        };

        var githubRepositoryNodes = PopulateGithubRepositories.RemoveNullRepositoryNodes(repositoryNodes);
        var githubTeamsByRepoName = new Dictionary<string, List<RepositoryTeam>>(StringComparer.OrdinalIgnoreCase)
        {
            { "repo1", [new RepositoryTeam("cdp-platform", "platform-team-id", "Platform")] }
        };

        var ex = Record.Exception(() =>
            PopulateGithubRepositories.BuildRepositories(githubRepositoryNodes, new HashSet<string>(), githubTeamsByRepoName)
        );

        Assert.Null(ex);

        var repositories =
            PopulateGithubRepositories.BuildRepositories(githubRepositoryNodes, new HashSet<string>(), githubTeamsByRepoName);
        Assert.Single(repositories);
        Assert.Equal("repo1", repositories[0].Id);
        Assert.Single(repositories[0].Teams);
        Assert.Equal("platform-team-id", repositories[0].Teams[0].TeamId);
    }

    [Fact]
    public void BuildRepositoriesFromRealGithubSearchFixtures()
    {
        var fixtureNames = new[]
        {
            "github-search-page-1.json",
            "github-search-page-2.json",
            "github-search-page-3.json"
        };

        var allNodes = fixtureNames
            .Select(ReadFixture)
            .Select(fixture => JsonSerializer.Deserialize<SearchRepoQueryResponse>(fixture))
            .SelectMany(response => response!.data!.search.nodes)
            .ToList();

        var githubRepositoryNodes = PopulateGithubRepositories.RemoveNullRepositoryNodes(allNodes);
        var repositories = PopulateGithubRepositories.BuildRepositories(
            githubRepositoryNodes,
            new HashSet<string>(),
            new Dictionary<string, List<RepositoryTeam>>()
        );

        var expected = new List<Repository>
        {
            Expected("cdp-dotnet-backend-template",
                "C# ASP.NET Minimial API template with MongoDB, FluentValidation, Swagger and Serilog logging",
                "C#", "https://github.com/DEFRA/cdp-dotnet-backend-template", false,
                "2023-08-24T07:08:56Z", ["backend", "cdp", "dotnet", "template"]),
            Expected("grants-ui", "Git repository for service grants-ui", "JavaScript",
                "https://github.com/DEFRA/grants-ui", false, "2025-03-19T10:08:04Z",
                ["cdp", "frontend", "node", "service"]),
            Expected("trade-imports-data-api", "Git repository for service trade-imports-data-api", "C#",
                "https://github.com/DEFRA/trade-imports-data-api", false, "2025-04-01T15:11:37Z",
                ["backend", "cdp", "dotnet", "service"]),
            Expected("forms-runner", "Git repository for service forms-runner", "JavaScript",
                "https://github.com/DEFRA/forms-runner", false, "2023-11-08T14:45:21Z",
                ["cdp", "frontend", "service"]),
            Expected("cdp-node-backend-template", "Core delivery platform Node.js Backend Template",
                "JavaScript", "https://github.com/DEFRA/cdp-node-backend-template", false,
                "2023-06-20T12:10:50Z", ["cdp", "template", "backend", "node"]),
            Expected("marine-licensing-backend-demo",
                "Git repository for service marine-licensing-backend-demo", "JavaScript",
                "https://github.com/DEFRA/marine-licensing-backend-demo", true, "2024-03-14T13:53:13Z",
                ["backend", "cdp", "node", "service"]),
            Expected("ai-model-test", "Git repository for service ai-model-test", "Python",
                "https://github.com/DEFRA/ai-model-test", false, "2025-03-20T16:39:21Z",
                ["backend", "cdp", "python", "service"]),
            Expected("ahwr-public-user-ui", "Git repository for service ahwr-public-user-ui", "JavaScript",
                "https://github.com/DEFRA/ahwr-public-user-ui", false, "2025-09-30T13:43:09Z",
                ["cdp", "frontend", "node", "service"]),
            Expected("disinfectant-frontend", "Git repository for service disinfectant-frontend",
                "JavaScript", "https://github.com/DEFRA/disinfectant-frontend", false,
                "2024-06-28T10:15:54Z", ["cdp", "frontend", "node", "service"]),
            Expected("nrf-library", "Git repository for nrf-library", "JavaScript",
                "https://github.com/DEFRA/nrf-library", false, "2026-04-28T14:07:53Z",
                ["cdp", "repository"]),
            Expected("epr-backend", "Git repository for service epr-backend", "JavaScript",
                "https://github.com/DEFRA/epr-backend", false, "2025-04-10T09:58:41Z",
                ["backend", "cdp", "node", "service"]),
            Expected("trade-imports-processor", "Git repository for service trade-imports-processor",
                "C#", "https://github.com/DEFRA/trade-imports-processor", false,
                "2025-04-01T15:13:45Z", ["backend", "cdp", "dotnet", "service"]),
            Expected("pha-import-notifications", "Git repository for service pha-import-notifications",
                "C#", "https://github.com/DEFRA/pha-import-notifications", false,
                "2024-10-29T11:04:22Z", ["backend", "cdp", "dotnet", "service"]),
            Expected("cdp-node-prototype-template", "Git repository for cdp-node-prototype-template",
                "Dockerfile", "https://github.com/DEFRA/cdp-node-prototype-template", false,
                "2025-07-18T12:04:13Z", ["cdp", "repository", "template", "prototype"]),
            Expected("grant-config-woodland", "Git repository for grant-config-woodland", "Shell",
                "https://github.com/DEFRA/grant-config-woodland", true, "2026-03-31T15:10:51Z",
                ["cdp", "repository"])
        };

        Assert.Equivalent(expected, repositories);
    }

    private static Repository Expected(
        string id,
        string description,
        string primaryLanguage,
        string url,
        bool isArchived,
        string createdAt,
        string[] topics) =>
        new()
        {
            Id = id,
            Description = description,
            PrimaryLanguage = primaryLanguage,
            Url = url,
            IsArchived = isArchived,
            CreatedAt = DateTimeOffset.Parse(createdAt),
            Teams = [],
            Topics = topics
        };

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Could not locate fixture file '{path}'");
        }

        return File.ReadAllText(path);
    }
}