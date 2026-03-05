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

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.SlackChannel))
            {
                logger.LogWarning("Invalid slack channel {Channel} for ruleId {RuleId}, skipping", rule.SlackChannel, rule.RuleId);
                continue;
            }
            
            if (!rule.IsEnabled)
            {
                logger.LogInformation("{Event} rule for {Entity}, ({RuleId}) is disabled, skipping", rule.EventType, rule.Entity, rule.RuleId);
                continue;
            }

            try
            {
                var message = notificationEvent.SlackMessage();
                await slackClient.SendToChannel(rule.SlackChannel, message, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch notification for RuleId {RuleId} to {SlackChannel}", rule.RuleId, rule.SlackChannel);
            }
        }
    }
}
