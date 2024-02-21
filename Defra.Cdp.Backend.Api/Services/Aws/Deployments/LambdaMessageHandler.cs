using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Tenants;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class LambdaMessageHandler
{
    private readonly IDeploymentsService _deploymentsService;
    private readonly ILogger<LambdaMessageHandler> _logger;

    public LambdaMessageHandler(IDeploymentsService deploymentsService, ILogger<LambdaMessageHandler> logger)
    {
        _deploymentsService = deploymentsService;
        _logger = logger;
    }
        
    // We need to link the requested deployment (before the deployment has started) to the actual deployment as
    // actioned by the lambda. 
    public async Task Handle(string id, EcsEvent ecsEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing lambda deployment message {Id}", id);
        // ID from cdp-self-service-ops
        var cdpDeploymentId = ecsEvent.CdpDeploymentId;
        
        // ID of ECS deployer
        var ecsSvcDeploymentId = ecsEvent.Detail.EcsSvcDeploymentId ?.Trim();

        if (!string.IsNullOrWhiteSpace(cdpDeploymentId) && !string.IsNullOrWhiteSpace(ecsSvcDeploymentId))
        {
            var d = await _deploymentsService.FindDeployment(cdpDeploymentId, cancellationToken);

            if (d != null)
            {
                var userName = d.User;
                if (string.IsNullOrWhiteSpace(userName) || userName == "n/a") userName = ecsEvent.DeployedBy;
                _logger.LogInformation($"Matching id {cdpDeploymentId} to deployer {ecsSvcDeploymentId}");
                var updatedDeployment = new Deployment
                {
                    Id = d.Id,
                    DeploymentId = d.DeploymentId,
                    Environment = d.Environment,
                    Service = d.Service,
                    Version = d.Version,
                    User = userName,
                    UserId = d.UserId,
                    DeployedAt = d.DeployedAt,
                    Status = d.Status,
                    DesiredStatus = "RUNNING",
                    DockerImage = d.DockerImage,
                    TaskId = d.TaskId,
                    InstanceTaskId = d.InstanceTaskId,
                    InstanceCount = d.InstanceCount,
                    EcsSvcDeploymentId = ecsSvcDeploymentId,
                    Cpu = d.Cpu,
                    Memory = d.Memory
                };
                await _deploymentsService.Insert(updatedDeployment, cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    $"couldn't find anything to match for {cdpDeploymentId} to deployer {ecsSvcDeploymentId} ");
            }
        }
        else
        {
            _logger.LogWarning("Could not match an ecs lambda deployment to an existing request");    
        }
    }

}