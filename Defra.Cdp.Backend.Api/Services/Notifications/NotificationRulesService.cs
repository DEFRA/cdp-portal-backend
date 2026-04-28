using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Usage;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public interface INotificationRuleService
{
    Task SaveAsync(NotificationRule rule, CancellationToken ct);
    Task UpdateAsync(NotificationRule rule, CancellationToken ct);
    Task<bool> DeleteAsync(string ruleId, CancellationToken ct);
    Task<NotificationRule?> FindRule(string ruleId, CancellationToken ct);
    Task<List<NotificationRule>> FindByEntity(string entity, CancellationToken ct);
    Task<List<NotificationRule>> FindMatchingRules(INotificationEvent notification, CancellationToken ct);
}

public class NotificationRuleService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) 
    : MongoService<NotificationRule>(connectionFactory, CollectionName, loggerFactory), INotificationRuleService, IStatsReporter
{
    private const string CollectionName = "notificationrules";

    protected override List<CreateIndexModel<NotificationRule>> DefineIndexes(IndexKeysDefinitionBuilder<NotificationRule> builder)
    {
        var uniqueRuleIdIdx = new CreateIndexModel<NotificationRule>(builder.Ascending(r => r.RuleId),
            new CreateIndexOptions { Unique = true });
        
        var matchLookupIdx = new CreateIndexModel<NotificationRule>(
            builder.Combine(
                builder.Ascending(r => r.EventType),
                builder.Ascending(r => r.Entity),
                builder.Ascending(r => r.Environments),
                builder.Ascending(r => r.IsEnabled)));
        return [uniqueRuleIdIdx, matchLookupIdx];
    }

    public async Task SaveAsync(NotificationRule rule, CancellationToken ct)
    {
        await Collection.InsertOneAsync(rule, new InsertOneOptions(), ct);
    }

    public async Task UpdateAsync(NotificationRule rule, CancellationToken ct)
    {
        await Collection.ReplaceOneAsync(r => r.RuleId == rule.RuleId, rule, new ReplaceOptions(), ct);
    }
    
    public async Task<bool> DeleteAsync(string ruleId, CancellationToken ct)
    {
        var result = await Collection.DeleteOneAsync(r => r.RuleId == ruleId, ct);
        return result.DeletedCount > 0;
    }
    
    public async Task<NotificationRule?> FindRule(string ruleId, CancellationToken ct)
    {
        return await Collection.Find(r => r.RuleId == ruleId).FirstOrDefaultAsync(ct);
    }
    
    public async Task<List<NotificationRule>> FindByEntity(string entity, CancellationToken ct)
    {
        return await Collection.Find(r => r.Entity == entity).ToListAsync(ct);
    }

    public async Task<List<NotificationRule>> FindMatchingRules(INotificationEvent notification, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<NotificationRule>();
        var filter = fb.Eq(r => r.EventType, notification.EventType);
        
        if (notification.Entity != null)
        {
            filter &= fb.Eq(r => r.Entity, notification.Entity);    
        }
        
        if (notification.Environment != null)
        {
            filter &= fb.AnyEq(r => r.Environments, notification.Environment);
        }

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task ReportStats(ICloudWatchMetricsService metrics, CancellationToken cancellationToken)
    {
        var totalEnabled = await Collection.CountDocumentsAsync(x => x.IsEnabled, null, cancellationToken);
        metrics.RecordCount("NotificationsEnabled", null, totalEnabled);
        
        var pipeline = new EmptyPipelineDefinition<NotificationRule>()
            .Group(x => x.EventType, g => new 
            { 
                EventType = g.Key, 
                Count = g.Count() 
            });

        var totalByEventType = await Collection.Aggregate(pipeline, null, cancellationToken).ToListAsync(cancellationToken);
        foreach (var r in totalByEventType)
        {
            metrics.RecordCount("NotificationsByType", new Dictionary<string, string>{ {"EventType", r.EventType} } , r.Count);
        }
    }
}
