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
                    Text = new TextObject { Type = "plain_text", Text = "Test Notification" }
                },
                new Block
                {
                    Type = "section",
                    Fields =
                    [
                        new TextObject
                        {
                            Type = "mrkdwn", 
                            Text = $"*Service/test-suite*: {EscapeMarkdown(entityName)}\n"
                        },
                        new TextObject
                        {
                            Type = "mrkdwn", 
                            Text = $"*Intended channel*: #{EscapeMarkdown(channel)}\n"
                        }
                    ]
                },
                new Block
                {
                    Type = "section",
                    Text = 
                        new TextObject
                        {
                            Type = "mrkdwn",
                            Text = "This message was triggered by the CDP Portal.\n\n" +
                                   "If it did not appear in the correct channel you may need to add the *CDP Bot* integration to the channel.\n\n" +
                                   "See the <https://portal.cdp-int.defra.cloud/documentation/how-to/testing/test-notifications.md#add-cdp-bot-to-the-channel|View in portal> documentation for more details."
                        }
                    
                }
            ]
        };
    }
}