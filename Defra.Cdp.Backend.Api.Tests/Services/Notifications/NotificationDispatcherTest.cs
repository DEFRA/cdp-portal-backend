using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class NotificationDispatcherTest
{
    private readonly ISlackClient _slackClient = Substitute.For<ISlackClient>();
    private readonly INotificationRuleService _ruleService = Substitute.For<INotificationRuleService>();
    private readonly ILogger<NotificationDispatcher> _logger = Substitute.For<ILogger<NotificationDispatcher>>();
    
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
            Environments = ["dev"]
        };

        var alert = new TestRunPassedEvent { Entity = entityId, Environment = "dev", RunId = "1234" };
        
        _ruleService
            .FindMatchingRules(Arg.Is(alert), Arg.Any<CancellationToken>())
            .Returns([alertInDevRule]);

        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient, _logger);
        await dispatcher.Dispatch(alert, ct);
        await _slackClient.Received().SendToChannel(Arg.Is(alertInDevRule.SlackChannel), Arg.Any<SlackMessageBody>(), ct);
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


        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient, _logger);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = entityId, Environment = "test", RunId = "1234" }, ct);
        await dispatcher.Dispatch(new TestRunPassedEvent { Entity = "anotherEntity", Environment = "dev", RunId = "1234"}, ct);
        await _slackClient.DidNotReceiveWithAnyArgs().SendToChannel(Arg.Any<string>(), Arg.Any<SlackMessageBody>(), ct);
    }

    [Fact]
    public async Task will_not_dispatch_when_channel_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;
        var alert = new TestRunPassedEvent { Entity = "foo-tests", Environment = "dev", RunId = "1234" };
        var rule = new NotificationRule
        {
            Entity = "foo-tests",
            EventType = NotificationTypes.TestPassed,
            SlackChannel = " ",
            IsEnabled = true
        };

        _ruleService.FindMatchingRules(Arg.Any<INotificationEvent>(), Arg.Any<CancellationToken>()).Returns([rule]);

        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient, _logger);
        await dispatcher.Dispatch(alert, ct);

        await _slackClient.DidNotReceiveWithAnyArgs().SendToChannel(Arg.Any<string>(), Arg.Any<SlackMessageBody>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task will_continue_dispatching_if_one_channel_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var alert = new TestRunPassedEvent { Entity = "foo-tests", Environment = "dev", RunId = "1234" };

        var badRule = new NotificationRule
        {
            Entity = "foo-tests",
            EventType = NotificationTypes.TestPassed,
            SlackChannel = "broken-channel",
            IsEnabled = true
        };
        var goodRule = new NotificationRule
        {
            Entity = "foo-tests",
            EventType = NotificationTypes.TestPassed,
            SlackChannel = "healthy-channel",
            IsEnabled = true
        };

        _ruleService.FindMatchingRules(Arg.Any<INotificationEvent>(), Arg.Any<CancellationToken>()).Returns([badRule, goodRule]);
        _slackClient.SendToChannel("broken-channel", Arg.Any<SlackMessageBody>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("send failed")));
        _slackClient.SendToChannel("healthy-channel", Arg.Any<SlackMessageBody>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var dispatcher = new NotificationDispatcher(_ruleService, _slackClient, _logger);
        await dispatcher.Dispatch(alert, ct);

        await _slackClient.Received(1).SendToChannel("healthy-channel", Arg.Any<SlackMessageBody>(), ct);
    }
}
