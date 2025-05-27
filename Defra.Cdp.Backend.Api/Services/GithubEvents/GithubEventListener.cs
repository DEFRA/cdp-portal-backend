using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.GithubEvents;

public class GithubEventListener(
    IAmazonSQS sqs,
    IOptions<GithubEventListenerOptions> listenerConfig,
    IOptions<GithubOptions> githubConfig,
    IGithubEventHandler eventHandler,
    ILogger<GithubEventListener> logger)
    : SqsListener(sqs, listenerConfig.Value.QueueUrl, logger)
{

    private List<string>? _webhooksToListenTo;

    private List<string> WebhooksToProcess()
    {
        if (_webhooksToListenTo == null)
        {
            _webhooksToListenTo =
            [
                githubConfig.Value.Repos.CdpAppDeployments,
                githubConfig.Value.Repos.CdpTfSvcInfra,
                githubConfig.Value.Repos.CdpAppConfig,
                githubConfig.Value.Repos.CdpNginxUpstreams,
                githubConfig.Value.Repos.CdpCreateWorkflows,
                githubConfig.Value.Repos.CdpSquidProxy,
                githubConfig.Value.Repos.CdpGrafanaSvc
            ];
        }

        return _webhooksToListenTo;
    }

    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {QueueUrl}: {Id}", QueueUrl, message.MessageId);

        try
        {
            await Handle(message, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to process message {Id} {Exception}", message.MessageId, e.Message);
        }
    }

    public async Task Handle(Message message, CancellationToken cancellationToken)
    {
        var eventWrapper = TryParseMessageBody(message.Body);
        if (eventWrapper != null && ShouldHandleMessage(eventWrapper))
            await eventHandler.Handle(eventWrapper, cancellationToken);
        else
            logger.LogInformation("Message from {QueueUrl}: {Id} was not readable: {Body}", QueueUrl,
                message.MessageId, message.Body);
    }

    private bool ShouldHandleMessage(GithubEventMessage githubEventMessage)
    {
        return githubEventMessage is { GithubEvent: "workflow_run", Repository.Name: not null } &&
               WebhooksToProcess().Contains(githubEventMessage.Repository.Name);
    }

    private GithubEventMessage? TryParseMessageBody(string body)
    {
        Console.WriteLine(body);
        try
        {
            return JsonSerializer.Deserialize<GithubEventMessage>(body);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to parse message body: {Exception} \n {body}", e.Message, body);
            return null;
        }
    }
}