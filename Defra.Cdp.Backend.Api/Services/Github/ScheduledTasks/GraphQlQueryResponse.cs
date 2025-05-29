namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public record PageInfo(
    bool hasNextPage,
    string endCursor
);

public record RepoQueryResponse(
    Data? data
);

public record Data(
    Organization organization
);

public record Organization(
    string id,
    Team? team
);

public record Team(
    Repositories repositories
);

public record Repositories(
    PageInfo pageInfo,
    IEnumerable<RepositoryNode> nodes
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
    bool isTemplate,
    bool isPrivate,
    DateTimeOffset createdAt
);

public record PrimaryLanguage(
    string name
);