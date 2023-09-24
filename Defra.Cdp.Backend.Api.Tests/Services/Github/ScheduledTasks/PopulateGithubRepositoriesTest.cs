using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using FluentAssertions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class PopulateGithubRepositoriesTest
{
    private Repository QueryToRepository(RepositoryResult repoResult, IEnumerable<string> teams)
    {
        return new Repository
        {
            Id = repoResult.Name,
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

    [Fact]
    public void ConvertGithubQueryResultToRepositoriesCorrectly()
    {
        var createdAt = DateTimeOffset.Now;
        var repoResult1 =
            new RepositoryResult("repo1", "desc1", "Javascript", "https://url1", false, false, true, createdAt);
        var repoResult2 = new RepositoryResult("repo2", "desc2", "C#", "https://url2", false, true, true, createdAt);
        var repoResult3 = new RepositoryResult("repo3", "desc3", "Java", "https://url3", false, true, false, createdAt);

        var fakeQueryResult =
            new List<TeamResult>
            {
                new("cdp-platform", new[] { repoResult1, repoResult3 }),
                new("fisheries", new[] { repoResult2, repoResult3 })
            };

        var actual = PopulateGithubRepositories.QueryResultToRepositories(fakeQueryResult);
        var expected = new List<Repository>
        {
            QueryToRepository(repoResult1, new[] { "cdp-platform" }),
            QueryToRepository(repoResult2, new[] { "fisheries" }),
            QueryToRepository(repoResult3, new[] { "cdp-platform", "fisheries" })
        };

        var r1 = actual.First(r => r.Id == "repo1");
        var r2 = actual.First(r => r.Id == "repo2");
        var r3 = actual.First(r => r.Id == "repo3");

        actual.Should().BeEquivalentTo(expected);
    }
}