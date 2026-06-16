using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public static class SubscriptionValidator
{
    public static async Task<List<string>> Validate(CreateTenantSubscription sub, IEntityResourceService entities, CreateTenantResourceRequest request, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        
        // Check queue owner exists
        var queueService = await entities.ServiceExists(sub.QueueService, cancellationToken);
        if (!queueService)
        {
            errors.Add($"SQS Subscription {sub.QueueService} to {sub.Topic} queue owner is an unknown service {sub.QueueService}");
        }
        
        // Check topic owner exists
        var topicService = await entities.ServiceExists(sub.TopicService, cancellationToken);
        if (!topicService)
        {
            errors.Add($"SQS Subscription {sub.QueueService} to {sub.Topic} topic owner is an unknown service {sub.QueueService}");
        }
        
        // Check queue exists
        var envs = CreateResourceEnvironments.ToCdpEnvironments(sub.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"SQS Subscription {sub.QueueService} to {sub.Topic} has an invalid or missing environment: {sub.Environments}");
        }
        
        // Assume that the form was populated from the existing entity data so queue will have .fifo if present
        var requestQueues = request.SqsQueues.Select(sqs => sqs.Name).ToList();

        var queueOwner = await entities.QueueExists(sub.Queue, envs, cancellationToken);
        if (queueOwner == null && !requestQueues.Contains(sub.Queue))
        {
            errors.Add($"SQS Subscription queue {sub.Queue} doesn't exist, check it is part of this request");    
        }
        
        // Assume that the form was populated from the existing entity data so topic will have .fifo if present
        var requestTopics = request.SnsTopics.Select(sns => sns.Fifo ? $"{sns.Name}.fifo" : sns.Name).ToList();

        var topicOwner = await entities.TopicExists(sub.Topic, envs, cancellationToken);
        if (topicOwner == null && !requestTopics.Contains(sub.Topic)) {
            errors.Add($"SQS Subscription topic {sub.Topic} doesn't exist, check it is part of this request");
        }
        return errors;
    }
}