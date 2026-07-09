using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IResourceRequestPrMergedHandler : IGithubWorkflowEventHandler;

public class ResourceRequestPrMergedHandler (
        IResourceRequestService resourceRequestService,
        ILogger<ResourceRequestPrMergedHandler> logger)
        : IResourceRequestPrMergedHandler
    {
        public string EventType => "resource-request-pr-merged";

        public async Task Handle(string message, CancellationToken cancellationToken)
        {
            var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ResourceRequestPrMergedPayload>>(message);
            if (workflowEvent == null)
            {
                logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
                return;
            }

            var payload = workflowEvent.Payload;
            var request = await resourceRequestService.UpdatePullRequestStatus(payload.PrNumber, PrStatus.Merged, cancellationToken);
            if (request != null)
            {
                logger.LogInformation("PR {PrNumber} merged for tenant resource request {RunId}", payload.PrNumber, request.Inputs?.RunId);
            }
        }

}