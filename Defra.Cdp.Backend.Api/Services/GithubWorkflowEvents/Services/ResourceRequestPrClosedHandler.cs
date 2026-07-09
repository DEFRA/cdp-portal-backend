using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IResourceRequestPrClosedHandler : IGithubWorkflowEventHandler;
    
public class ResourceRequestPrClosedHandler (
        IResourceRequestService resourceRequestService,
        ILogger<ResourceRequestPrClosedHandler> logger)
        : IResourceRequestPrClosedHandler
    {
        public string EventType => "resource-request-pr-closed";

        public async Task Handle(string message, CancellationToken cancellationToken)
        {
            var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ResourceRequestPrClosedPayload>>(message);
            if (workflowEvent == null)
            {
                logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
                return;
            }

            var payload = workflowEvent.Payload;
            var request = await resourceRequestService.UpdatePullRequestStatus(payload.PrNumber, PrStatus.Closed, cancellationToken);
            if (request != null)
            {
                logger.LogInformation("PR {PrNumber} closed for tenant resource request {RunId}", payload.PrNumber, request.Inputs?.RunId);
            }
        }

}