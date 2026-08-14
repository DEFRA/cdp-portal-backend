using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static SlackMessageBody DeploymentSuccessTemplate(DeploymentSuccessEvent e)
    {
        var deploymentUri = new UriBuilder(PortalPublicUrl.BaseUri()) { Path = $"/deployments/{e.Environment}/{e.DeploymentId}" };
        var fields = new List<TextObject>
        {
            new() { Type = "mrkdwn", Text = $"*Environment:*\n{EscapeMarkdown(e.Environment ?? "")}" },
            new() { Type = "mrkdwn", Text = $"*Performed by:*\n{EscapeMarkdown(e.UserDisplayName ?? "Unknown")}" }
        };

        if (!string.IsNullOrWhiteSpace(e.PreviousVersion) && e.PreviousVersion != e.Version)
        {
            var compareUri = $"https://github.com/DEFRA/{Uri.EscapeDataString(e.Entity)}/compare/{Uri.EscapeDataString(e.PreviousVersion)}...{Uri.EscapeDataString(e.Version)}";
            fields.Add(new TextObject
            {
                Type = "mrkdwn",
                Text =
                    $"*Version:*\n~{EscapeMarkdown(e.PreviousVersion)}~ → *{EscapeMarkdown(e.Version)}*\n<{compareUri}|Compare on GitHub>"
            });
        }

        fields.Add(new TextObject
        {
            Type = "mrkdwn",
            Text = $"*View details:*\n <{deploymentUri.Uri.AbsoluteUri}|Open in portal>"
        });

        return new SlackMessageBody
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = $"✅ {e.Entity}:{e.Version} Deployment succeeded", Emoji = true }
                },
                new Block
                {
                    Type = "section",
                    Fields = fields
                }
            ]
        };
    }
}
