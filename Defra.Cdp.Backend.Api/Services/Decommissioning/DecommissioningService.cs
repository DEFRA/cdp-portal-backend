using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Decommissioning;

public sealed class DecommissioningService(
    ILoggerFactory loggerFactory,
    IEntitiesService entitiesService,
    IDeploymentsService deploymentsService,
    SelfServiceOpsClient selfServiceOpsClient
) : IJob
{
    private readonly ILogger<DecommissioningService> _logger = loggerFactory.CreateLogger<DecommissioningService>();

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Polling pending service decommissions...");
        var pendingEntities = await entitiesService.EntitiesPendingDecommission(context.CancellationToken);
        foreach (var entity in pendingEntities)
        {
            _logger.LogInformation("Decommissioning entity {EntityName} in status {Status}", entity.Name,
                entity.Status);
            var deployments =
                await deploymentsService.RunningDeploymentsForService(entity.Name, context.CancellationToken);
            if (deployments.Count > 0 && deployments.Any(d => d.Status != DeploymentStatus.Undeployed))
            {
                _logger.LogWarning("Entity {EntityName} has running deployments, will try next time...", entity.Name);
                continue;
            }

            if (entity.Decommissioned is not { WorkflowsTriggered: true })
            {
                await selfServiceOpsClient.TriggerDecommissionWorkflow(entity.Name, context.CancellationToken);
                await entitiesService.DecommissioningWorkflowTriggered(entity.Name, context.CancellationToken);
            }
            else
            {
                _logger.LogWarning("Entity {EntityName} has resources that are not ready for decommissioning.",
                    entity.Name);
            }
        }
    }
}