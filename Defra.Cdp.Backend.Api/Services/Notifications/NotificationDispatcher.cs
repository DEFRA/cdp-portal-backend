using Defra.Cdp.Backend.Api.Services.Notifications.Slack;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public interface INotificationDispatcher
{
    Task Dispatch(INotificationEvent notificationEvent, CancellationToken ct);
}

public class NotificationDispatcher(
    INotificationRuleService notificationRules,
    ISlackClient slackClient,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task Dispatch(INotificationEvent notificationEvent, CancellationToken ct)
    {
        var rules = await notificationRules.FindMatchingRules(notificationEvent, ct);

        if (rules.Count == 0) return;
        var message = notificationEvent.SlackMessage();

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.SlackChannel))
            {
                continue;
            }

            try
            {
                await slackClient.SendToChannel(rule.SlackChannel, message, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch notification for RuleId {RuleId} to {SlackChannel}", rule.RuleId, rule.SlackChannel);
            }
        }
    }
}
