using System.Text.Json;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Snow.Models;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Defra.Cdp.Backend.Api.Services.Snow;

public interface ISnowDeploymentTriggerService
{
    Task TriggerDeploymentRecord(ServiceStatusChange statusChange, CancellationToken cancellationToken);
}

public class SnowDeploymentTriggerService(
    ITriggerWorkflowService triggerWorkflowService,
    IEntitiesService entitiesService,
    IDeploymentsService deploymentsService,    
    IOptions<SnowOptions> snowOptions,
    ILogger<SnowDeploymentTriggerService> logger) : ISnowDeploymentTriggerService
{
    private const string Repo = "cdp-deployments-snow";
    private const string SnowWorkflowTriggerEventAction = "snow-deployment-record-triggered";

    public async Task TriggerDeploymentRecord(ServiceStatusChange statusChange, CancellationToken cancellationToken)
    {
        if (!snowOptions.Value.Workflows.TryGetValue(statusChange.Environment, out var workflow))
        {
            return;
        }

        var entity = await entitiesService.GetEntity(statusChange.EntityId, cancellationToken);

        // Decided: for services with multiple owning teams, just report the first one (by team ID) to ServiceNow.
        var teamName = entity?.Teams
            .OrderBy(team => team.TeamId, StringComparer.OrdinalIgnoreCase)
            .Select(team => string.IsNullOrWhiteSpace(team.Name) ? team.TeamId : team.Name)
            .FirstOrDefault()
            ?? SnowPayloadDefaults.Unknown;

        var portalPayload = new SnowPortalPayload
        {
            Service = statusChange.EntityId,
            Version = statusChange.Version,
            Environment = statusChange.Environment,
            LastDeploymentStatus = statusChange.NewStatus,
            CdpDeploymentId = statusChange.DeploymentId,
            Updated = DateTime.UtcNow,
            User = string.IsNullOrWhiteSpace(statusChange.UserDisplayName)
                ? null
                : new SnowPortalUserPayload { DisplayName = statusChange.UserDisplayName }
        };

        var extendedPayload = new SnowExtendedPayload
        {
            TeamName = teamName,
            PreviousVersion = statusChange.PreviousVersion
        };

        var inputs = new SnowDeploymentWorkflowInputs(
            JsonSerializer.Serialize(portalPayload),
            JsonSerializer.Serialize(extendedPayload)
        );

        using (LogContext.PushProperty("event.action", SnowWorkflowTriggerEventAction))
        {
            
            logger.LogInformation(
                "Triggering SNOW deployment record for {Service} {Version} in {Environment}, deployment {DeploymentId}, team {TeamName}",
                statusChange.EntityId,
                statusChange.Version,
                statusChange.Environment,
                statusChange.DeploymentId,
                teamName);

            var response =
                await triggerWorkflowService.TriggerWorkflow(Repo, workflow, inputs, cancellationToken);

            await deploymentsService.UpdateServiceNowChangeRequest(
                new ServiceNowChangeRequest
                {
                    CdpDeploymentId = statusChange.DeploymentId,
                    Service = statusChange.EntityId,
                    Status = "requested",
                    WorkflowRunUrl = response?.WorkflowRunUrl
                }, cancellationToken);

            logger.LogInformation(
                "Triggered SNOW workflow for deployment {DeploymentId}: response body present {HasWorkflowResponse}, run id {WorkflowRunId}, run url {WorkflowRunUrl}",
                statusChange.DeploymentId,
                response != null,
                response?.WorkflowRunId,
                response?.WorkflowRunUrl);
        }
    }
}