using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IResourceRequestPrEventHandler : IGithubWorkflowEventHandler;

public class ResourceRequestPrEventHandler(
    IResourceRequestService resourceRequestService,
    ISlackClient slackClient,
    IConfiguration configuration,
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
        var linkedRequest = await resourceRequestService.AttachPullRequest(
            payload.RunId,
            new ResourceRequestPullRequest { Url = payload.PrUrl, Number = payload.PrNumber },
            cancellationToken);

        if (linkedRequest == null)
        {
            logger.LogWarning("No resource request found for runId {RunId}", payload.RunId);
            return;
        }

        var channel = configuration["TenantResourceRequested:SlackChannel"];
        if (string.IsNullOrWhiteSpace(channel))
        {
            logger.LogWarning("No Slack channel configured for tenant resource requested notifications (TenantResourceRequested:SlackChannel)");
            return;
        }

        var notificationEvent = new TenantResourceRequestedEvent
        {
            ServiceName = linkedRequest.EntityName,
            RequestedByDisplayName = linkedRequest.RequestedBy?.DisplayName,
            RequestedByUserId = linkedRequest.RequestedBy?.Id,
            PullRequestUrl = payload.PrUrl,
            PullRequestNumber = payload.PrNumber,
            WorkflowRunUrl = payload.WorkflowRunUrl
        };

        await slackClient.SendToChannel(channel, notificationEvent.SlackMessage(), cancellationToken);
    }
}