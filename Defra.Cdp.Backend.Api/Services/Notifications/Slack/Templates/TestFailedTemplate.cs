using System.Text.Json;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static string TestFailedTemplate(TestRunFailedEvent e)
    {
        var reportUrlBuilder = new UriBuilder(PortalPublicUrl.BaseUri()) { Path = $"/test-suites/test-results/{e.Environment}/failed/{e.Entity}/{e.RunId}/index.html" };
        
        var payload = new SlackMessageTemplate
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = $"❌ {e.Entity}: Test run failed", Emoji = true }
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
                            Text = $"View the full report: <{reportUrlBuilder.Uri.AbsoluteUri}|Open in portal>"
                        }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(payload, s_jsonOptions);
    }
}