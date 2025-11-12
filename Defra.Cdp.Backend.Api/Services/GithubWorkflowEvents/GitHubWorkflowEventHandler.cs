using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

public interface IGithubWorkflowEventHandler
{
    Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class GithubWorkflowEventHandler(
    IAppConfigVersionsService appConfigVersionsService,
    IAppConfigsService appConfigsService,
    ILogger<GithubWorkflowEventHandler> logger)
    : IGithubWorkflowEventHandler
{
    public async Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken)
    {
        switch (eventWrapper.EventType)
        {
            case "app-config-version":
                await HandleEvent(eventWrapper, messageBody, appConfigVersionsService, cancellationToken);
                break;
            case "app-config":
                await HandleEvent(eventWrapper, messageBody, appConfigsService, cancellationToken);
                break;
            default:
                logger.LogInformation("Ignoring event: {EventType} not handled {Message}", eventWrapper.EventType, messageBody);
                break;
        }
    }

    private async Task HandleEvent<T>(CommonEventWrapper eventWrapper, string messageBody, IEventsPersistenceService<T> service,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling event: {EventType}", eventWrapper.EventType);
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<T>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogInformation("Failed to parse Github workflow event - message: {MessageBody}", messageBody);
            return;
        }

        await service.PersistEvent(workflowEvent, cancellationToken);
    }
}