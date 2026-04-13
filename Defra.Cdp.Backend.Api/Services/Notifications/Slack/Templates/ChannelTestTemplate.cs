namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static SlackMessageBody ChannelTestTemplate(string entityName, string channel)
    {
        return new SlackMessageBody
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = $"Portal notification check for: {entityName}" }
                },
                new Block
                {
                    Type = "section",
                    Fields =
                    [
                        new TextObject
                        {
                            Type = "mrkdwn", 
                            Text = $"*Service/test-suite: {EscapeMarkdown(entityName)}\n"
                        },
                        new TextObject
                        {
                            Type = "mrkdwn", 
                            Text = $"*Intended channel: {EscapeMarkdown(channel)}\n"
                        },
                        new TextObject
                        {
                            Type = "mrkdwn",
                            Text = "This message was triggered by the CDP Portal.\n" +
                                   "If it was not delivered to the correct channel you need to add the 'CDP Bot' integration.\n" +
                                   "See the [docs](https://portal.cdp-int.defra.cloud/documentation/how-to/testing/test-notifications.md#add-cdp-bot-to-the-channel) for more information."
                        }
                    ]
                }
            ]
        };
    }
}