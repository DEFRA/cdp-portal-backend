using Defra.Cdp.Backend.Api.Services.Entities;

namespace Defra.Cdp.Backend.Api.Services.Sboms;

public interface ISbomServiceOwnershipHandler
{
    Task Handle(CancellationToken ct);
}

public class SbomServiceOwnershipHandler(ISbomExplorerClient client, IEntitiesService entitiesService, ILogger<SbomServiceOwnershipHandler> logger) : ISbomServiceOwnershipHandler
{
    public async Task Handle(CancellationToken ct)
    {
        try
        {
            var entities = await entitiesService.GetEntities(new EntityMatcher { },
                new EntitySearchOptions { Summary = true }, ct);
            await client.PushTeams(entities, ct);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to push entity team data to SBOM explorer: {Error}", exception.Message);
        }
    }
}