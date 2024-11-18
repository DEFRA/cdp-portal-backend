using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using FluentAssertions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class PopulateGithubRepositoriesTest
{
    private Repository QueryToRepository(RepositoryResult repoResult, IEnumerable<RepositoryTeam> teams)
    {
        return new Repository
        {
            Id = repoResult.Name,
            Topics = repoResult.Topics.nodes.Select(t => t.topic.name),
            CreatedAt = repoResult.CreatedAt,
            Description = repoResult.Description,
            IsArchived = repoResult.IsArchived,
            IsPrivate = repoResult.IsPrivate,
            IsTemplate = repoResult.IsTemplate,
            PrimaryLanguage = repoResult.PrimaryLanguage,
            Url = repoResult.Url,
            Teams = teams
        };
    }

    private Repository QueryToRepository(RepositoryNode repoNode, IEnumerable<RepositoryTeam> teams)
    {
        return new Repository
        {
            Id = repoNode.name,
            CreatedAt = repoNode.createdAt,
            Description = repoNode.description,
            IsArchived = repoNode.isArchived,
            IsPrivate = repoNode.isPrivate,
            IsTemplate = repoNode.isTemplate,
            PrimaryLanguage = repoNode.primaryLanguage.name,
            Url = repoNode.url,
            Teams = teams
        };
    }

    [Fact]
    public void ConvertGithubQueryResultToRepositoriesCorrectly()
    {
        var mockTopics = Topics.CreateMockTopics();

        var createdAt = DateTimeOffset.Now;
        var repoResult1 =
            new RepositoryResult("repo1", mockTopics, "desc1", "Javascript", "https://url1", false, false,
                true, createdAt);
        var repoResult2 = new RepositoryResult("repo2", mockTopics, "desc2", "C#", "https://url2", false, true,
            true, createdAt);
        var repoResult3 = new RepositoryResult("repo3", mockTopics, "desc3", "Java", "https://url3", false,
            true, false, createdAt);

        var fakeQueryResult =
            new List<TeamResult>
            {
                new("cdp-platform", new[] { repoResult1, repoResult3 }),
                new("fisheries", new[] { repoResult2, repoResult3 })
            };
        var githubTeamToTeamIdMap =
            new Dictionary<string, string> { { "cdp-platform", "1111" }, { "fisheries", "2222" } };
        var githubTeamToTeamNameMap =
            new Dictionary<string, string> { { "cdp-platform", "CDP Team" }, { "fisheries", "Fisheries" } };
        var actual =
            PopulateGithubRepositories.QueryResultToRepositories(fakeQueryResult, githubTeamToTeamIdMap,
                githubTeamToTeamNameMap);
        var expected = new List<Repository>
        {
            QueryToRepository(repoResult1, new[] { new RepositoryTeam("cdp-platform", "1111", "CDP Team") }),
            QueryToRepository(repoResult2, new[] { new RepositoryTeam("fisheries", "2222", "Fisheries") }),
            QueryToRepository(repoResult3,
                new[]
                {
                    new RepositoryTeam("cdp-platform", "1111", "CDP Team"),
                    new RepositoryTeam("fisheries", "2222", "Fisheries")
                })
        };

        var r1 = actual.First(r => r.Id == "repo1");
        var r2 = actual.First(r => r.Id == "repo2");
        var r3 = actual.First(r => r.Id == "repo3");

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ConvertHttpGraphResultToRepositoriesCorrectly()
    {
        var mockTopics = Topics.CreateMockTopics();

        var createdAt = DateTimeOffset.Now;

        var repoNod1 =
            new RepositoryNode("repo1", mockTopics, "desc1", new PrimaryLanguage("Javascript"), "https://url1",
                false, false, true,
                createdAt);
        var repoNod2 = new RepositoryNode("repo2", mockTopics, "desc2", new PrimaryLanguage("C#"),
            "https://url2", false, true,
            true, createdAt);
        var repoNod3 = new RepositoryNode("repo3", mockTopics, "desc3", new PrimaryLanguage("Java"),
            "https://url3", false, true,
            false, createdAt);

        var fakeQueryR =
            new QueryResponse(
                new Data(
                    new Organization(
                        "some-id",
                        new Teams(
                            null!,
                            new List<TeamNodes>
                            {
                                new(
                                    "cdp-platform",
                                    new Repositories(
                                        new List<RepositoryNode> { repoNod1, repoNod3 }
                                    )
                                ),
                                new(
                                    "fisheries",
                                    new Repositories(
                                        new List<RepositoryNode> { repoNod2, repoNod3 }
                                    )
                                )
                            }
                        )
                    )));
        var githubTeamToTeamIdMap =
            new Dictionary<string, string> { { "cdp-platform", "1111" }, { "fisheries", "2222" } };
        var githubTeamToTeamNameMap =
            new Dictionary<string, string> { { "cdp-platform", "CDP Team" }, { "fisheries", "Fisheries" } };
        var actual = PopulateGithubRepositories
            .QueryResultToRepositories(fakeQueryR, githubTeamToTeamIdMap, githubTeamToTeamNameMap).ToList();

        var repoResult1 =
            new RepositoryResult("repo1", mockTopics, "desc1", "Javascript", "https://url1", false, false,
                true, createdAt);
        var repoResult2 = new RepositoryResult("repo2", mockTopics, "desc2", "C#", "https://url2", false, true,
            true, createdAt);
        var repoResult3 = new RepositoryResult("repo3", mockTopics, "desc3", "Java", "https://url3", false,
            true, false, createdAt);

        var expected = new List<Repository>
        {
            QueryToRepository(repoResult1, new[] { new RepositoryTeam("cdp-platform", "1111", "CDP Team") }),
            QueryToRepository(repoResult2, new[] { new RepositoryTeam("fisheries", "2222", "Fisheries") }),
            QueryToRepository(repoResult3,
                new[]
                {
                    new RepositoryTeam("cdp-platform", "1111", "CDP Team"),
                    new RepositoryTeam("fisheries", "2222", "Fisheries")
                })
        };

        var r1 = actual.First(r => r.Id == "repo1");
        var r2 = actual.First(r => r.Id == "repo2");
        var r3 = actual.First(r => r.Id == "repo3");

        actual.Should().BeEquivalentTo(expected);
    }
    
    
    [Fact]
    public void FiltersOutTeamsNotLinkedToCDPTeams()
    {
        var mockTopics = Topics.CreateMockTopics();

        var createdAt = DateTimeOffset.Now;
        var repoResult1 =
            new RepositoryResult("repo1", mockTopics, "desc1", "Javascript", "https://url1", false, false,
                true, createdAt);
        var fakeQueryResult =
            new List<TeamResult>
            {
                new("cdp-platform", [repoResult1]),
                new("non-cdp-team", [repoResult1])
            };
        var githubTeamToTeamIdMap =
            new Dictionary<string, string> { { "cdp-platform", "1111" } };
        var githubTeamToTeamNameMap =
            new Dictionary<string, string> { { "cdp-platform", "CDP Team" } };
        var actual =
            PopulateGithubRepositories.QueryResultToRepositories(fakeQueryResult, githubTeamToTeamIdMap,
                githubTeamToTeamNameMap).ToList();

       Assert.Single(actual);
       Assert.Single(actual[0].Teams);
       Assert.Equal("1111", actual[0].Teams.First().TeamId);
    }

}