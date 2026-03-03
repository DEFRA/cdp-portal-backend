using System.Text.Json;
using Amazon.SimpleNotificationService;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack;

public interface ISlackClient
{
    Task SendToChannel(string channel, SlackMessageBody messageBody,CancellationToken ct); // Richer block style slack messages
}

public class SlackLambdaClient(IAmazonSimpleNotificationService snsClient, IOptions<SlackLambdaOptions> options, ILogger<SlackLambdaClient> logger) : ISlackClient
{
    private async Task Send(SlackMessagePayload payload, CancellationToken ct)
    {
        var messageBody = JsonSerializer.Serialize(payload);
        if (!options.Value.Enabled)
        {
          
            logger.LogInformation("Disabled: Would send '{Msg}' to {Channel}",  messageBody, payload.Message.Channel);
            return;
        }
       
        await snsClient.PublishAsync(options.Value.TopicArn, messageBody, ct);
    }

    public async Task SendToChannel(string channel, SlackMessageBody body, CancellationToken ct)
    {
        var msg = new SlackMessagePayload
        {
            Message = new SlackMessagePayload.SlackMessage
            {
                Channel = channel,
                Blocks = body.Blocks,
                Text = body.Text
            },
            Team = channel
        };
        await Send(msg, ct);
    }
}