using System.Net;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class SlackLambdaClientTest
{
    [Fact]
    public async Task Should_publish_send_slack_notification_mono_lambda_event()
    {
        using var _ = new ScopedEnvironmentVariable("ENVIRONMENT", "dev");

        var sns = Substitute.For<IAmazonSimpleNotificationService>();
        var config = Options.Create(new MonoLambdaOptions
        {
            QueueUrl = "http://queue.url",
            Enabled = true,
            TopicArn = "arn:aws:sns:region:account-id:topic-name.fifo"
        });

        var trigger = new MonoLambdaTrigger(sns, config, NullLogger<MonoLambdaTrigger>.Instance);
        var slackClient = new SlackLambdaClient(trigger, NullLogger<SlackLambdaClient>.Instance);
        sns.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { HttpStatusCode = HttpStatusCode.OK });

        var body = new SlackMessageBody
        {
            Text = "Deployment complete",
            Blocks =
            [
                new Block
                {
                    Type = "section",
                    Text = new TextObject { Type = "mrkdwn", Text = "Done" }
                }
            ]
        };

        await slackClient.SendToChannel("cdp-alerts", body, TestContext.Current.CancellationToken);

        await sns.Received(1).PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>());

        var publishCall = sns.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IAmazonSimpleNotificationService.PublishAsync));
        var request = Assert.IsType<PublishRequest>(publishCall.GetArguments()[0]);

        Assert.Equal("arn:aws:sns:region:account-id:topic-name.fifo", request.TopicArn);
        Assert.Equal("dev", request.MessageGroupId);
        AssertPayload(request.Message);
    }

    private static void AssertPayload(string json)
    {
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(root.TryGetProperty("event_type", out var eventType));
        Assert.Equal("send_slack_notification", eventType.GetString());

        Assert.True(root.TryGetProperty("payload", out var payload));

        Assert.True(payload.TryGetProperty("team", out var team));
        Assert.Equal("platform", team.GetString());

        Assert.True(payload.TryGetProperty("message", out var message));

        Assert.True(message.TryGetProperty("channel", out var channel));
        Assert.Equal("cdp-alerts", channel.GetString());

        Assert.True(message.TryGetProperty("text", out var text));
        Assert.Equal("Deployment complete", text.GetString());

        Assert.True(message.TryGetProperty("blocks", out var blocks));
        Assert.Equal(1, blocks.GetArrayLength());
    }

    private sealed class ScopedEnvironmentVariable : IDisposable
    {
        private readonly string _key;
        private readonly string? _previous;

        public ScopedEnvironmentVariable(string key, string value)
        {
            _key = key;
            _previous = System.Environment.GetEnvironmentVariable(key);
            System.Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(_key, _previous);
        }
    }
}
