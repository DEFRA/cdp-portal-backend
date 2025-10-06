using System.Diagnostics;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntityStatusService
{
    Task<EntityStatus?> GetEntityStatus(string repositoryName, CancellationToken cancellationToken);

    Task UpdatePendingEntityStatuses(CancellationToken cancellationToken);
}

public class EntityStatusService(
    IEntitiesService entitiesService,
    IRepositoryService repositoryService,
    ITenantServicesService tenantServicesService,
    ISquidProxyConfigService squidProxyService,
    INginxUpstreamsService nginxUpstreamsService,
    IAppConfigsService appConfigsService,
    IGrafanaDashboardsService grafanaDashboardsService,
    ILogger<EntityStatusService> logger
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

        var resources = await ResourcesForRepositoryName(repositoryName, entity, cancellationToken);

        return new EntityStatus(entity, resources);
    }

    private async Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken)
    {
        var entityStatus = await GetEntityStatus(repositoryName, cancellationToken);
        if (entityStatus == null)
        {
            return;
        }

        logger.LogInformation("Current state: {EntityStatus}",
            string.Join(", ", entityStatus.Resources.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
        var overallStatus = OverallStatus(entityStatus);
        logger.LogInformation("Updating overall status for {RepositoryName} to {OverallStatus}", repositoryName,
            overallStatus);
        await entitiesService.UpdateStatus(overallStatus, repositoryName, cancellationToken);
    }

    private static Status OverallStatus(EntityStatus entityStatus)
    {
        var allTrue = entityStatus.Resources.Values.All(v => v);
        return allTrue ? Status.Created : Status.Creating;
    }

    public async Task UpdatePendingEntityStatuses(CancellationToken cancellationToken)
    {
        var creatingEntities = await entitiesService.GetCreatingEntities(cancellationToken);
        logger.LogInformation("Updating {CreatingEntitiesCount} pending entity statuses...", creatingEntities.Count);
        foreach (var entity in creatingEntities)
        {
            logger.LogInformation("Updating status for entity: {EntityName}", entity.Name);
            await UpdateOverallStatus(entity.Name, cancellationToken);
        }
    }

    private async Task<Dictionary<string, bool>> ResourcesForRepositoryName(string repositoryName, Entity entity,
        CancellationToken cancellationToken)
    {
        var resourcesList = GetResourceServicesForEntityType(entity.Type);

        var tasks = resourcesList.Select(async service =>
        {
            var exists = await service.ExistsForRepositoryName(repositoryName, cancellationToken);
            return new KeyValuePair<string, bool>(service.ResourceName(), exists);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(kv => kv.Key, kv => kv.Value);
    }


    private List<IResourceService> GetResourceServicesForEntityType(Type entityType)
    {
        return entityType switch
        {
            Type.Repository => [repositoryService],
            Type.TestSuite =>
            [
                repositoryService,
                tenantServicesService,
                squidProxyService,
                appConfigsService
            ],
            Type.Microservice =>
            [
                repositoryService,
                tenantServicesService,
                squidProxyService,
                nginxUpstreamsService,
                appConfigsService,
                grafanaDashboardsService
            ]
        };
    }
}