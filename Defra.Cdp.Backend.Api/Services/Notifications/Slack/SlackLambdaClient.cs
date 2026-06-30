using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack;

public interface ISlackClient
{
    Task SendToChannel(string channel, SlackMessageBody messageBody,CancellationToken ct); // Richer block style slack messages
}

public class SlackLambdaClient(MonoLambdaTrigger monoLambdaTrigger, ILogger<SlackLambdaClient> logger) : ISlackClient
{
    private static string ResolveEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "management";
    }

    public async Task SendToChannel(string channel, SlackMessageBody body, CancellationToken ct)
    {
        var payload = new SlackMessagePayload
        {
            Message = new SlackMessagePayload.SlackMessage
            {
                Channel = channel,
                Blocks = body.Blocks,
                Text = body.Text
            },
            Team = "platform"
        };

        var triggerEvent = new MonoLambdaTriggerEvent<SlackMessagePayload>
        {
            EventType = "send_slack_notification",
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };

        var environment = ResolveEnvironmentName();
        logger.LogInformation("Publishing Slack notification via mono-lambda for channel {Channel} in {Environment}", channel, environment);
        await monoLambdaTrigger.Trigger(triggerEvent, environment, ct);
    }
}