using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static SlackMessageBody ShutteredTemplate(ShutteredEvent e)
    {
        return ShutteringTemplate(
            eventName: "shuttered",
            headerPrefix: "🟧",
            actor: e.ActionedByDisplayName,
            entity: e.Entity,
            environment: e.Environment,
            url: e.Url);
    }

    public static SlackMessageBody UnshutteredTemplate(UnshutteredEvent e)
    {
        return ShutteringTemplate(
            eventName: "unshuttered",
            headerPrefix: "✅",
            actor: e.ActionedByDisplayName,
            entity: e.Entity,
            environment: e.Environment,
            url: e.Url);
    }

    private static SlackMessageBody ShutteringTemplate(
        string eventName,
        string headerPrefix,
        string? actor,
        string entity,
        string? environment,
        string url)
    {
        var maintenanceUri = new UriBuilder(PortalPublicUrl.BaseUri()) { Path = $"/services/{entity}/maintenance" };

        return new SlackMessageBody
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject
                    {
                        Type = "plain_text",
                        Text = $"{headerPrefix} {entity} has been {eventName}",
                        Emoji = true
                    }
                },
                new Block
                {
                    Type = "section",
                    Fields =
                    [
                        new TextObject { Type = "mrkdwn", Text = $"*Environment:*\n{EscapeMarkdown(environment ?? "")}" },
                        new TextObject { Type = "mrkdwn", Text = $"*URL:*\n{EscapeMarkdown(url)}" },
                        new TextObject { Type = "mrkdwn", Text = $"*Actioned by:*\n{EscapeMarkdown(actor ?? "Unknown")}" },
                        new TextObject
                        {
                            Type = "mrkdwn",
                            Text = $"*View details:*\n<{maintenanceUri.Uri.AbsoluteUri}|Open in portal>"
                        }
                    ]
                }
            ]
        };
    }
}
