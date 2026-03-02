using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class NotificationDispatcherTest
{
    ISlackClient slackClient = Substitute.For<ISlackClient>();
    INotificationRuleService ruleService = Substitute.For<INotificationRuleService>();
    
    [Fact]
    public async Task will_dispatch_based_on_rules()
    {
        var ct = TestContext.Current.CancellationToken;

        var entityId = "foo-tests";
        var alertInDevRule = new NotificationRule
        {
            Entity = entityId, 
            EventType = NotificationEventTypes.TestRunPassed.Type,
            SlackChannel = "foo-non-prod-alerts",
            IsEnabled = true,
            Conditions = new Dictionary<string, string>()
            {
                { "Environment", "dev" }
            }
        };
        ruleService
            .FindByEntityAndTypeAsync(Arg.Is(entityId), Arg.Is(NotificationEventTypes.TestRunPassed.Type), Arg.Any<CancellationToken>())
            .Returns([alertInDevRule]);

        var dispatcher = new NotificationDispatcher(ruleService, slackClient);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = entityId, Environment = "dev", RunId = "1234"}, ct);
        await slackClient.Received().SendText(Arg.Is(alertInDevRule.SlackChannel), Arg.Any<string>(), ct);
    }
    
    [Fact]
    public async Task will_not_dispatch_if_criteria_doesnt_match()
    {
        var ct = TestContext.Current.CancellationToken;

        var entityId = "foo-tests";
        var alertInDevRule = new NotificationRule
        {
            Entity = entityId, 
            EventType = NotificationEventTypes.TestRunPassed.Type,
            SlackChannel = "foo-non-prod-alerts",
            IsEnabled = true,
            Conditions = new Dictionary<string, string>()
            {
                { "Environment", "dev" }
            }
        };
        ruleService.FindByEntityAndTypeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        ruleService
            .FindByEntityAndTypeAsync(Arg.Is(entityId), Arg.Is(NotificationEventTypes.TestRunPassed.Type), Arg.Any<CancellationToken>())
            .Returns([alertInDevRule]);


        var dispatcher = new NotificationDispatcher(ruleService, slackClient);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = entityId, Environment = "test", RunId = "1234"}, ct);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = "anotherEntity", Environment = "dev", RunId = "1234"}, ct);
        await slackClient.DidNotReceiveWithAnyArgs().SendText(Arg.Any<string>(), Arg.Any<string>(), ct);
    }
}