namespace Defra.Cdp.Backend.Api.Services.Entities;

public enum EntitySortBy
{
    Name,
    Team
}

public record EntitySearchOptions(
    bool Summary = false, // Exclude the envs section from the results for performance
    EntitySortBy SortBy = EntitySortBy.Name
);