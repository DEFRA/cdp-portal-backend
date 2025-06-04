using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class PopulateGithubRepositoriesTest
{
    [Fact]
    public void AddMultipleTeamsToRepositories()
    {
        var topics = Topics.CreateMockTopics();
        var dateTimeNow = DateTimeOffset.Now;
        var repositories = PopulateGithubRepositories.GroupRepositoriesByTeam(
            new Dictionary<UserServiceTeam, List<RepositoryNode>>
            {
                {
                    new UserServiceTeam("Platform", "PlatformTeam", "cdp-platform", "2022-01-01T00:00:00Z", "2023-01-01T00:00:00Z", "platform-team-id", []),
                    [
                        new RepositoryNode("repo1", topics, "desc1", new PrimaryLanguage("Javascript"),
                            "https://url1", false, false, true, dateTimeNow),

                        new RepositoryNode("repo3", topics, "desc3", new PrimaryLanguage("Java"),
                            "https://url3", false, true, false, dateTimeNow)
                    ]
                },
                {
                    new UserServiceTeam("Fisheries", "The fisheries team", "fisheries", "2022-01-01T00:00:00Z", "2023-01-01T00:00:00Z", "fisheries-team-id", []),
                    [
                        
                        new RepositoryNode("repo2", topics, "desc2", new PrimaryLanguage("C#"),
                            "https://url2", false, true, true, dateTimeNow),
                        new RepositoryNode("repo3", topics, "desc3", new PrimaryLanguage("Java"),
                            "https://url3", false, true, false, dateTimeNow)
                    ]
                }
            }, new LoggerFactory().CreateLogger<PopulateGithubRepositories>());

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
                IsPrivate = true,
                IsTemplate = false,
                PrimaryLanguage = "Javascript",
                Url = "https://url1",
                Teams = [new RepositoryTeam("cdp-platform", "platform-team-id", "Platform")]
            },
            new()
            {
                Id = "repo2",
                Topics = topicNames,
                CreatedAt = dateTimeNow,
                Description = "desc2",
                IsArchived = false,
                IsPrivate = true,
                IsTemplate = true,
                PrimaryLanguage = "C#",
                Url = "https://url2",
                Teams = [new RepositoryTeam("fisheries", "fisheries-team-id", "Fisheries")]
            },
            new()
            {
                Id = "repo3",
                Topics = topicNames,
                CreatedAt = dateTimeNow,
                Description = "desc3",
                IsArchived = false,
                IsPrivate = false,
                IsTemplate = true,
                PrimaryLanguage = "Java",
                Url = "https://url3",
                Teams =
                [
                    new RepositoryTeam("cdp-platform", "platform-team-id", "Platform"),
                    new RepositoryTeam("fisheries", "fisheries-team-id", "Fisheries")
                ]
            }
        };
        repositories.Should().BeEquivalentTo(expected);
    }
}