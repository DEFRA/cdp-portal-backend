using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IResourceRequestFailedHandler : IGithubWorkflowEventHandler;

public class ResourceRequestFailedHandler(
    IResourceRequestService resourceRequestService,
    ILogger<ResourceRequestFailedHandler> logger)
    : IResourceRequestFailedHandler
{
    public string EventType => "resource-request-failed";

    public async Task Handle(string message, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ResourceRequestFailedPayload>>(message);
        if (workflowEvent == null)
        {
            logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
            return;
        }

        var payload = workflowEvent.Payload;
        var request = await resourceRequestService.MarkFailed(payload.RunId, cancellationToken);
        if (request != null)
        {
            logger.LogInformation(
                "Workflow failed for tenant resource request {RunId} ({WorkflowRunUrl})",
                payload.RunId,
                payload.WorkflowRunUrl);
        }
    }
}
