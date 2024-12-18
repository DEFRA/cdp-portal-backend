using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

public interface IGitHubEventHandler
{
    Task Handle(GitHubWorkflowEventType eventType, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class GitHubWorkflowEventHandler(
    IAppConfigVersionService appConfigVersionService,
    IVanityUrlsService vanityUrlsService,
    ILogger<GitHubWorkflowEventHandler> logger)
    : IGitHubEventHandler
{
    public async Task Handle(GitHubWorkflowEventType eventType, string messageBody, CancellationToken cancellationToken)
    {
        switch (eventType.EventType)
        {
            case "app-config-version":
                await HandleEvent(eventType, messageBody, appConfigVersionService, cancellationToken);
                break;
            case "nginx-vanity-urls":
                await HandleEvent(eventType, messageBody, vanityUrlsService, cancellationToken);
                break;
            default:
                logger.LogInformation("Ignoring event: {Event} not handled", eventType.EventType);
                return;
        }
    }

    private async Task HandleEvent<T>(GitHubWorkflowEventType eventType, string messageBody, IEventsPersistenceService<T> service,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling event: {Event}", eventType.EventType);
        var workflowEvent = JsonSerializer.Deserialize<Event<T>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogInformation("Failed to parse GitHub workflow event - message: {}", messageBody);
            return;
        }

        await service.PersistEvent(workflowEvent, cancellationToken);
    }
}