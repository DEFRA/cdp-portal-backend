using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class SubscriptionValidator : ICreateResourceValidator<CreateTenantSubscription>
{
    public async Task<List<string>> Validate(CreateTenantSubscription sub, ResourceValidatorContext ctx, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        var entities = ctx.EntitiesCollection;
        
        // Check queue owner exists
        var queueService = await entities.Find(e => e.Name == sub.QueueService).Project(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (queueService == null)
        {
            errors.Add($"SQS Subscription {sub.QueueService} to {sub.Topic} queue owner is an unknown service {sub.QueueService}");
        }
        
        // Check topic owner exists
        var topicService = await entities.Find(e => e.Name == sub.TopicService).Project(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (topicService == null)
        {
            errors.Add($"SQS Subscription {sub.QueueService} to {sub.Topic} topic owner is an unknown service {sub.QueueService}");
        }
        
        // Check queue exists
        var fb = new FilterDefinitionBuilder<Entity>();
        var envs = CreateResourceEnvironments.ToCdpEnvironments(sub.Environments);
                
        var requestQueues = ctx.OriginalRequest.SqsQueues.Select(sqs => sqs.Name).ToList();

        foreach (var env in envs)
        {
            // Assume that the form was populated from the existing entity data so queue will have .fifo if present
            var sqsFilter = fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.sqsQueues.name"), sub.Queue);
            var queueOwner = await entities.Find(sqsFilter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
            if (queueOwner != null) continue;
            
            if(requestQueues.Contains(sub.Queue)) continue;
            
            errors.Add($"SQS Subscription queue {sub.Queue} doesn't exist in {env}");
            break;
        }
        
        var requestTopics = ctx.OriginalRequest.SnsTopics.Select(sns => sns.Fifo ? $"{sns.Name}.fifo" : sns.Name).ToList();
        foreach (var env in envs)
        {
            // Assume that the form was populated from the existing entity data so topic will have .fifo if present
            var snsFilter = fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.snsTopics.name"), sub.Topic);
            var topicOwner = await entities.Find(snsFilter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
            if (topicOwner != null) continue;
            
            if(requestTopics.Contains(sub.Topic)) continue;
            
            errors.Add($"SQS Subscription topic {sub.Topic} doesn't exist in {env}");
            break;
        }

        return errors;
    }
}