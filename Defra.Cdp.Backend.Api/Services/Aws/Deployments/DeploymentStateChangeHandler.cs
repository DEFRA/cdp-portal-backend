using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.DeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.TestSuites;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandler
{
    private readonly IDeploymentsServiceV2 _deploymentsService;
    private readonly ITestRunService _testRunService;
    private readonly IDeploymentTriggerService _deploymentTriggerService;
    private readonly ILogger<DeploymentStateChangeEventHandler> _logger;

    public DeploymentStateChangeEventHandler(
      IDeploymentsServiceV2 deploymentsService, 
      ITestRunService testRunService,
      IDeploymentTriggerService deploymentTriggerService, 
      ILogger<DeploymentStateChangeEventHandler> logger
   )
    {
        _deploymentsService = deploymentsService;
      _testRunService = testRunService;
      _deploymentTriggerService = deploymentTriggerService;
        _logger = logger;
    }

    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{id} Handling EcsDeploymentStateChange Update {deploymentId}, {name} {reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var updated = await _deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason, cancellationToken);
        if (!updated)
        {
            _logger.LogWarning("{id} Failed to record EcsDeploymentStateChange {deploymentId}",  id, ecsEvent.Detail.DeploymentId);    
        }
      else
      {
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
               var testRun = TestRun.FromDeployment(deployment, trigger.TestSuite);
               await _testRunService.CreateTestRun(testRun, cancellationToken);
               deployment.DeploymentTestRuns.Add(testRun);
               await _deploymentsService.UpdateDeployment(deployment, cancellationToken);
            }
         }
      }
    }
}