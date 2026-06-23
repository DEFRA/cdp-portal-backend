using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IResourceRequestPrEventHandler : IGithubWorkflowEventHandler;

public class ResourceRequestPrEventHandler(
    IResourceRequestService resourceRequestService,
    ILogger<ResourceRequestPrEventHandler> logger)
    : IResourceRequestPrEventHandler
{
    public string EventType => "resource-request-pr";

    public async Task Handle(string message, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ResourceRequestPrPayload>>(message);
        if (workflowEvent == null)
        {
            logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
            return;
        }

        var payload = workflowEvent.Payload;
        var linked = await resourceRequestService.AttachPullRequest(
            payload.RunId,
            new ResourceRequestPullRequest { Url = payload.PrUrl, Number = payload.PrNumber },
            cancellationToken);

        if (!linked) logger.LogWarning("No resource request found for runId {RunId}", payload.RunId);
    }
}