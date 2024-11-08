using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.DeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.TestSuites;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public class DeploymentTriggerEventHandler
{
   private readonly IDeploymentsServiceV2 _deploymentsService;
   private readonly IDeploymentTriggerService _deploymentTriggerService;
   private readonly SelfServiceOpsFetcher _selfServiceOpsFetcher;
   private readonly ILogger<DeploymentTriggerEventHandler> _logger;

   public DeploymentTriggerEventHandler(
     IConfiguration configuration,
     IDeploymentsServiceV2 deploymentsService,
     IDeploymentTriggerService deploymentTriggerService,
     ILogger<DeploymentTriggerEventHandler> logger
  )
   {
      _deploymentsService = deploymentsService;
      _deploymentTriggerService = deploymentTriggerService;
      _logger = logger;
      _selfServiceOpsFetcher = new SelfServiceOpsFetcher(configuration);
   }

   public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
   {
      _logger.LogInformation("{id} Handling EcsDeploymentStateChange trigger test runs for {deploymentId}, {name} {reason}",
       id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);

      var deployment = await _deploymentsService.FindDeploymentByLambdaId(ecsEvent.Detail.DeploymentId, cancellationToken);
      if (deployment == null)
      {
         _logger.LogWarning("{id} Deployment {deploymentId} not found", id, ecsEvent.Detail.DeploymentId);
      }
      else if (deployment.Status != DeploymentStatus.Running)
      {
         _logger.LogWarning("{id} Deployment {deploymentId} not running", id, ecsEvent.Detail.DeploymentId);
      }
      else
      {
         var deploymentTriggers = await _deploymentTriggerService.FindTriggersForDeployment(deployment, cancellationToken);

         foreach (var trigger in deploymentTriggers)
         {
            _logger.LogInformation("{id} Triggering test run for {deploymentId} {testSuite}", id, ecsEvent.Detail.DeploymentId, trigger.TestSuite);

            await _selfServiceOpsFetcher.triggerTestSuite(trigger.TestSuite, deployment.Environment, deployment.User, cancellationToken);

         }
      }
   }
}
