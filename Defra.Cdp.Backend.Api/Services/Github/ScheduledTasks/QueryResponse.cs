namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public record QueryResponse(
    Data data
);

public record Data(
    Organization organization
);

public record Organization(
    string id,
    Teams teams
);

public record Teams(
    PageInfo pageInfo,
    IEnumerable<TeamNodes> nodes
);

public record PageInfo(
    bool hasNextPage,
    string endCursor
);

public record TeamNodes(
    string slug,
    Repositories repositories
);

public record Repositories(
    IEnumerable<RepositoryNode> nodes
);

public record RepositoryNode(
    string name,
    string description,
    PrimaryLanguage primaryLanguage,
    string url,
    bool isArchived,
    bool isTemplate,
    bool isPrivate,
    DateTimeOffset createdAt
);

public record PrimaryLanguage(
    string name
);