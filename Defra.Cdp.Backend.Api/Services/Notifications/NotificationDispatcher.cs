using Defra.Cdp.Backend.Api.Services.Notifications.Slack;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public interface INotificationDispatcher
{
    Task Dispatch(INotificationEvent notificationEvent, CancellationToken ct);
}

public class NotificationDispatcher(INotificationRuleService notificationRules, ISlackClient slackClient) : INotificationDispatcher
{
    public async Task Dispatch(INotificationEvent notificationEvent, CancellationToken ct)
    {
        var rules = await notificationRules.FindMatchingRules(notificationEvent, ct);

        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            if (rule.SlackChannel != null)
            {
                await slackClient.SendText(rule.SlackChannel, notificationEvent.SlackMessage(), ct);
            }
        }
    }
}