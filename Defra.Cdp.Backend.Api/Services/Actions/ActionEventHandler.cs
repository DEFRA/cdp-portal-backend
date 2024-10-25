using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Actions.events;

namespace Defra.Cdp.Backend.Api.Services.Actions;

public interface IActionEventHandler
{
    Task Handle(ActionMessage header, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class ActionEventHandler(
    IAppConfigVersionService appConfigVersionService,
    ILogger<ActionEventHandler> logger)
    : IActionEventHandler
{
    public async Task Handle(ActionMessage header, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling action event: " + header.Action);
        switch (header.Action)
        {
            case "app-config-version":
                await HandleAppConfigVersion(header, cancellationToken);
                break;
            default:
                logger.LogDebug("Ignoring action: {Action} not handled", header.Action);
                return;
        }
    }

    private async Task HandleAppConfigVersion(ActionMessage header, CancellationToken cancellationToken)
    {
        var content = header.Content.Deserialize<AppConfigVersionMessageContent>();
        if (content == null)
        {
            logger.LogInformation("Failed to parse 'app-config-version' message");
            return;
        }

        logger.LogInformation("HandleAppConfigVersion: Persisting message {CommitSha} {CommitTimestamp} {Environment}",
            content.CommitSha, content.CommitTimestamp, content.Environment);
        await appConfigVersionService.SaveMessage(content.CommitSha, content.CommitTimestamp, content.Environment, cancellationToken);
    }

    public static ActionMessage? TryParseMessageBody(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ActionMessage>(body);
        }
        catch (Exception e)
        {
            return null;
        }
    }
}