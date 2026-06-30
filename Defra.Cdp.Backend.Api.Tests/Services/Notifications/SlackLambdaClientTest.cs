using System.Net;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
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
        var previousEnvironment = Environment.GetEnvironmentVariable("ENVIRONMENT");
        Environment.SetEnvironmentVariable("ENVIRONMENT", "dev");
        try
        {
            var sns = Substitute.For<IAmazonSimpleNotificationService>();
            var config = new OptionsWrapper<Defra.Cdp.Backend.Api.Config.MonoLambdaOptions>(
                new Defra.Cdp.Backend.Api.Config.MonoLambdaOptions
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

            await sns.Received(1).PublishAsync(Arg.Is<PublishRequest>(request =>
                    request.TopicArn == "arn:aws:sns:region:account-id:topic-name.fifo" &&
                    request.MessageGroupId == "dev" &&
                    HasExpectedPayload(request.Message)),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENVIRONMENT", previousEnvironment);
        }
    }

    private static bool HasExpectedPayload(string json)
    {
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        if (!root.TryGetProperty("event_type", out var eventType) || eventType.GetString() != "send_slack_notification")
        {
            return false;
        }

        if (!root.TryGetProperty("payload", out var payload))
        {
            return false;
        }

        if (!payload.TryGetProperty("team", out var team) || team.GetString() != "platform")
        {
            return false;
        }

        if (!payload.TryGetProperty("message", out var message))
        {
            return false;
        }

        if (!message.TryGetProperty("channel", out var channel) || channel.GetString() != "cdp-alerts")
        {
            return false;
        }

        if (!message.TryGetProperty("text", out var text) || text.GetString() != "Deployment complete")
        {
            return false;
        }

        return message.TryGetProperty("blocks", out var blocks) && blocks.GetArrayLength() == 1;
    }
}
