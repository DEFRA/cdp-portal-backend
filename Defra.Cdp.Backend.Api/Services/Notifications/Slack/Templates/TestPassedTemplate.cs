using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static SlackMessageBody TestPassedTemplate(TestRunPassedEvent e)
    {
        var reportUrlBuilder = new UriBuilder(PortalPublicUrl.BaseUri()) { Path = $"/test-suites/test-results/{e.Environment}/failed/{e.Entity}/{e.RunId}/index.html" };
        
        return new SlackMessageBody
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = $"✅ {e.Entity}: Test run passed", Emoji = true }
                },
                new Block
                {
                    Type = "section",
                    Fields =
                    [
                        new TextObject { Type = "mrkdwn", Text = $"*Environment:*\n{EscapeMarkdown(e.Environment ?? "")}" },
                        new TextObject
                        {
                            Type = "mrkdwn",
                            Text = $"*Test results*:\\n <{reportUrlBuilder.Uri.AbsoluteUri}|Open in portal>"
                        }
                    ]
                }
            ]
        };
    }
}