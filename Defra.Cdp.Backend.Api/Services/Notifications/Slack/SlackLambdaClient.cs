using System.Text.Json;
using Amazon.SimpleNotificationService;
using Defra.Cdp.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack;

public interface ISlackClient
{
    Task SendText(string channel, string text, CancellationToken ct); // Plain text message
    Task SendBlock(string channel, string text, CancellationToken ct); // Richer block style slack messages
}

public class SlackLambdaClient(IAmazonSimpleNotificationService snsClient, IOptions<SlackLambdaOptions> options, ILogger<SlackLambdaClient> logger) : ISlackClient
{
    private async Task Send(SlackMessagePayload payload, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Disabled: Would send {Msg} to {Channel}", payload.Message.Blocks ?? payload.Message.Text ?? "", payload.Message.Channel);
            return;
        }
        var messageBody = JsonSerializer.Serialize(payload);
        await snsClient.PublishAsync(options.Value.TopicArn, messageBody, ct);
    }

    public async Task SendText(string channel, string text, CancellationToken ct)
    {
        var msg = new SlackMessagePayload
        {
            Message = new SlackMessagePayload.SlackMessage
            {
                Channel = channel,
                Text = text
            },
            Team = channel // Not sure if this actually does anything in the lambda anymore aside for logging
        };
        await Send(msg, ct);
    }

    public async Task SendBlock(string channel, string block, CancellationToken ct)
    {
        var msg = new SlackMessagePayload
        {
            Message = new SlackMessagePayload.SlackMessage
            {
                Channel = channel,
                Blocks = block
            },
            Team = channel
        };
        await Send(msg, ct);
    }
}