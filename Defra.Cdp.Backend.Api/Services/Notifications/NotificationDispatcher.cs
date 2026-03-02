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
        var rules = await notificationRules.FindByEntityAndTypeAsync(notificationEvent.Entity, notificationEvent.EventType, ct);
        var matchingRules = rules.Where(rule => rule.IsEnabled && RuleConditionsMatch(rule, notificationEvent.Context)).ToList();

        if (matchingRules.Count == 0) return;

        foreach (var rule in matchingRules)
        {
            if (rule.SlackChannel == null) continue;
            await slackClient.SendText(rule.SlackChannel, notificationEvent.Message(), ct);
        }
    }
    
    private static bool RuleConditionsMatch(NotificationRule rule, Dictionary<string, string> eventContext)
    {
        if (rule.Conditions.Count == 0) return true;

        foreach (var condition in rule.Conditions)
        {
            if (!eventContext.TryGetValue(condition.Key, out var eventValue) || 
                !eventValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true; 
    }
}