using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class Topics
{
    public static RepositoryTopics CreateMockTopics()
    {
        var repositoryTopics = new List<RepositoryTopicNode>();
        repositoryTopics.Add(new RepositoryTopicNode(new Topic("cdp")));

        return new RepositoryTopics(repositoryTopics);
    }

    public static RepositoryTopics CreateMockEmptyTopics()
    {
        return new RepositoryTopics(Enumerable.Empty<RepositoryTopicNode>());
    }
}