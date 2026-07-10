using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Decommissioning;

public sealed class DecommissioningService(
    ILoggerFactory loggerFactory,
    IEntitiesService entitiesService,
    IDeploymentsService deploymentsService,
    ISelfServiceOpsClient selfServiceOpsClient,
    IAutoTestRunTriggerService autoTestRunTriggerService,
    IAutoDeploymentTriggerService autoDeploymentTriggerService,
    IMongoLock mongoLock
) : IJob
{
    private readonly ILogger<DecommissioningService> _logger = loggerFactory.CreateLogger<DecommissioningService>();
    private const string MongoLockName = "DecomissionServiceLock";


    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var acquiredLock = await mongoLock.Lock(MongoLockName, TimeSpan.FromSeconds(60), context.CancellationToken);
            if (!acquiredLock)
            {
                return;
            }

            try
            {
                var pendingEntities = await entitiesService.EntitiesPendingDecommission(context.CancellationToken);
                foreach (var entity in pendingEntities)
                {
                    _logger.LogInformation("Decommissioning entity {EntityName} in status {Status}", entity.Name,
                        entity.Status);
                    var deployments =
                        await deploymentsService.RunningDeploymentsForService(entity.Name,
                            context.CancellationToken);
                    if (deployments.Count > 0 && deployments.Any(d => d.Status != DeploymentStatus.Undeployed))
                    {
                        _logger.LogWarning("Entity {EntityName} has running deployments, will try next time...",
                            entity.Name);
                        continue;
                    }

                    if (entity.Decommissioned is not { WorkflowsTriggered: true })
                    {
                        await selfServiceOpsClient.TriggerDecommissionWorkflow(entity.Name,
                            context.CancellationToken);
                        await entitiesService.DecommissioningWorkflowTriggered(entity.Name,
                            context.CancellationToken);
                        await autoTestRunTriggerService.DecommissioningWorkflowTriggered(entity.Name,
                            context.CancellationToken);
                        await autoDeploymentTriggerService.DecommissioningWorkflowTriggered(entity.Name,
                            context.CancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Entity {EntityName} has resources that are not ready for decommissioning.",
                            entity.Name);
                    }
                }
            }
            finally
            {
                await mongoLock.Unlock(MongoLockName, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling pending service decommissions");
        }
    }
}