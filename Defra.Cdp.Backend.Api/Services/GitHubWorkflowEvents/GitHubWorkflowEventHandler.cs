using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

public interface IGitHubEventHandler
{
    Task Handle(GitHubWorkflowEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class GitHubWorkflowEventHandler(
    IAppConfigVersionService appConfigVersionService,
    IVanityUrlsService vanityUrlsService,
    ISquidProxyConfigService squidProxyConfigService,
    ILogger<GitHubWorkflowEventHandler> logger)
    : IGitHubEventHandler
{
    public async Task Handle(GitHubWorkflowEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken)
    {
        switch (eventWrapper.EventType)
        {
            case "app-config-version":
                await HandleEvent(eventWrapper, messageBody, appConfigVersionService, cancellationToken);
                break;
            case "nginx-vanity-urls":
                await HandleEvent(eventWrapper, messageBody, vanityUrlsService, cancellationToken);
                break;
            case "squid-proxy-config":
                await HandleEvent(eventWrapper, messageBody, squidProxyConfigService, cancellationToken);
                break;
            default:
                logger.LogInformation($"Ignoring event: {eventWrapper.EventType} not handled {messageBody}");
                return;
        }
    }

    private async Task HandleEvent<T>(GitHubWorkflowEventWrapper eventWrapper, string messageBody, IEventsPersistenceService<T> service,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"Handling event: {eventWrapper.EventType}");
        var workflowEvent = JsonSerializer.Deserialize<Event<T>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogInformation($"Failed to parse GitHub workflow event - message: {messageBody}");
            return;
        }

        await service.PersistEvent(workflowEvent, cancellationToken);
    }
}