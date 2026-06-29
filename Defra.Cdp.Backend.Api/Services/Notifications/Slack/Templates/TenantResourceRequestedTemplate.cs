namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

public static partial class SlackMessageTemplates
{
    public static SlackMessageBody TenantResourceRequestedTemplate(TenantResourceRequestedEvent e)
    {
        var requester = e.RequestedByDisplayName ?? e.RequestedByUserId ?? "Unknown";
        var workflowRun = e.WorkflowRunUrl == null
            ? "*Workflow run:*\nUnavailable"
            : $"*Workflow run:*\n<{e.WorkflowRunUrl}|Open run>";

        return new SlackMessageBody
        {
            Blocks =
            [
                new Block
                {
                    Type = "header",
                    Text = new TextObject { Type = "plain_text", Text = "📥 Tenant resource requested", Emoji = true }
                },
                new Block
                {
                    Type = "section",
                    Fields =
                    [
                        new TextObject { Type = "mrkdwn", Text = $"*Requested by:*\n{EscapeMarkdown(requester)}" },
                        new TextObject { Type = "mrkdwn", Text = $"*Service:*\n{EscapeMarkdown(e.ServiceName)}" },
                        new TextObject { Type = "mrkdwn", Text = $"*Pull request:*\n<{e.PullRequestUrl}|#{e.PullRequestNumber}>" },
                        new TextObject { Type = "mrkdwn", Text = workflowRun }
                    ]
                }
            ]
        };
    }
}
