namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public record PageInfo(
    bool hasNextPage,
    string? endCursor
);

public record SearchRepoQueryResponse(
    SearchData? data
);

public record SearchData(
    SearchResults search
);

public record SearchResults(
    PageInfo pageInfo,
    IEnumerable<RepositoryNode?> nodes
);

public record RepositoryTopics(
    IEnumerable<RepositoryTopicNode> nodes
);

public record RepositoryTopicNode(
    Topic topic
);

public record Topic(
    string name
);

public record RepositoryNode(
    string name,
    RepositoryTopics repositoryTopics,
    string description,
    PrimaryLanguage? primaryLanguage,
    string url,
    bool isArchived,
    DateTimeOffset createdAt
);

public record PrimaryLanguage(
    string name
);