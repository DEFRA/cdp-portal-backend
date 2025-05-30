using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntityStatusService
{
    Task<EntityStatus?> GetEntityStatus(string repositoryName, CancellationToken cancellationToken);

    Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken);
    Task UpdatePendingEntityStatuses(CancellationToken cancellationToken);
}

public class EntityStatusService(
    IEntitiesService entitiesService,
    IRepositoryService repositoryService,
    ITenantServicesService tenantServicesService,
    ISquidProxyConfigService squidProxyService,
    INginxUpstreamsService nginxUpstreamsService,
    IAppConfigsService appConfigsService,
    IGrafanaDashboardsService grafanaDashboardsService
)
    : IEntityStatusService
{
    public async Task<EntityStatus?> GetEntityStatus(string repositoryName, CancellationToken cancellationToken)
    {
        var entity = await entitiesService.GetEntity(repositoryName, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        var resources = await ResourcesForRepositoryName(repositoryName, cancellationToken, entity);

        return new EntityStatus(entity, resources);
    }

    public async Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken)
    {
        var entityStatus = await GetEntityStatus(repositoryName, cancellationToken);
        if (entityStatus == null)
        {
            return;
        }

        var allTrue = entityStatus.Resources.Values.All(v => v);
        var overallStatus = allTrue ? Status.Created : Status.Creating;
        await entitiesService.UpdateStatus(overallStatus, repositoryName, cancellationToken);
    }

    public async Task UpdatePendingEntityStatuses(CancellationToken cancellationToken)
    {
        var creatingEntities = await entitiesService.GetCreatingEntities(cancellationToken);
        foreach (var entity in creatingEntities)
        {
            await UpdateOverallStatus(entity.Name, cancellationToken);
        }
    }

    private async Task<Dictionary<string, bool>> ResourcesForRepositoryName(string repositoryName, CancellationToken cancellationToken, Entity entity)
    {
        List<IResourceService> resourcesList = GetResourceServicesForEntityType(entity.Type);

        var tasks = resourcesList.Select(async service =>
        {
            var exists = await service.ExistsForRepositoryName(repositoryName, cancellationToken);
            return new KeyValuePair<string, bool>(service.ResourceName(), exists);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(kv => kv.Key, kv => kv.Value);
    }


    private List<IResourceService> GetResourceServicesForEntityType(Model.Type entityType)
    {
        return entityType switch
        {
            Model.Type.Repository => [repositoryService],
            Model.Type.TestSuite =>
            [
                repositoryService,
                tenantServicesService,
                squidProxyService,
                appConfigsService
            ],
            Model.Type.Microservice =>
            [
                repositoryService,
                tenantServicesService,
                squidProxyService,
                nginxUpstreamsService,
                appConfigsService,
                grafanaDashboardsService
            ],
            _ => []
        };
    }
}