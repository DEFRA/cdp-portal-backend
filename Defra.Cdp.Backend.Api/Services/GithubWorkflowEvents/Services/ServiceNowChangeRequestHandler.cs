using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IServiceNowChangeRequestService : IGithubWorkflowEventHandler;

public class ServiceNowChangeRequestHandler(IDeploymentsService deploymentsService, ILoggerFactory loggerFactory)
    : IServiceNowChangeRequestService
{
    private readonly ILogger _logger =
        loggerFactory.CreateLogger<ServiceNowChangeRequestHandler>();

    public string EventType => "servicenow-change-request";

    public async Task Handle(string message, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ServiceNowChangeRequestPayload>>(message);
        if (workflowEvent == null)
        {
            _logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
            return;
        }

        var payload = workflowEvent.Payload;

        var changeRequest = new ServiceNowChangeRequest
        {
            Status = payload.Status,
            CdpDeploymentId = payload.CdpDeploymentId,
            Service = payload.Service,
            WorkflowRunUrl = payload.WorkflowRunUrl,
            ChangeRequestNumber = payload.ChangeRequestNumber,
            FailureStage = payload.FailureStage,
            FailureReason = payload.FailureReason,
            Attempt = payload.Attempt,
            MaxAttempts = payload.MaxAttempts
        };

        var updated = await deploymentsService.UpdateServiceNowChangeRequest(changeRequest, cancellationToken);

        if (!updated)
        {
            _logger.LogWarning(
                "Deployment {CdpDeploymentId} not found for ServiceNow change request",
                changeRequest.CdpDeploymentId);
        }
    }
}