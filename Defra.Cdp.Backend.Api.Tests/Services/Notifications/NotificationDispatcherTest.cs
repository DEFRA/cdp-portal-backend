using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class NotificationDispatcherTest
{
    private readonly ISlackClient _slackClient = Substitute.For<ISlackClient>();
    private readonly INotificationRuleService _ruleService = Substitute.For<INotificationRuleService>();
    
    [Fact]
    public async Task will_dispatch_based_on_rules()
    {
        var ct = TestContext.Current.CancellationToken;

        var entityId = "foo-tests";
        var alertInDevRule = new NotificationRule
        {
            Entity = entityId, 
            EventType = NotificationTypes.TestPassed,
            SlackChannel = "foo-non-prod-alerts",
            IsEnabled = true,
            Environment = "dev"
        };

        var alert = new TestRunPassedEvent { Entity = entityId, Environment = "dev", RunId = "1234" };
        
        _ruleService
            .FindMatchingRules(Arg.Is(alert), Arg.Any<CancellationToken>())
            .Returns([alertInDevRule]);

        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient);
        await dispatcher.Dispatch(alert, ct);
        await _slackClient.Received().SendText(Arg.Is(alertInDevRule.SlackChannel), Arg.Any<string>(), ct);
    }
    
    [Fact]
    public async Task will_not_dispatch_if_criteria_doesnt_match()
    {
        var ct = TestContext.Current.CancellationToken;

        var entityId = "foo-tests";
        
        _ruleService.FindMatchingRules(Arg.Any<INotificationEvent>(), Arg.Any<CancellationToken>()).Returns([]);
        _ruleService
            .FindMatchingRules(Arg.Any<INotificationEvent>(), Arg.Any<CancellationToken>())
            .Returns([]);


        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = entityId, Environment = "test", RunId = "1234" }, ct);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = "anotherEntity", Environment = "dev", RunId = "1234"}, ct);
        await _slackClient.DidNotReceiveWithAnyArgs().SendText(Arg.Any<string>(), Arg.Any<string>(), ct);
    }
}