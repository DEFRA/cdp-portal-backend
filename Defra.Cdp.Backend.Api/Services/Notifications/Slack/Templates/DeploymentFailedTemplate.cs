using System.Text.Json;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static string DeploymentFailedTemplate(DeploymentFailedEvent e)
    {
        var deploymentUri = new UriBuilder(PortalPublicUrl.BaseUri()) { Path = $"/deployments/{e.Environment}/{e.DeploymentId}" };
        
        var payload = new SlackMessageTemplate
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = $"❌ {e.Entity}:{e.Version} Deployment failed", Emoji = true }
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
                            Text = $"View details: <{deploymentUri.Uri.AbsoluteUri}|Open in portal>"
                        }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(payload, s_jsonOptions);
    }
}