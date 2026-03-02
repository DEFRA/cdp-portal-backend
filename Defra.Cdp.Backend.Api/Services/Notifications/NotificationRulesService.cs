using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public interface INotificationRuleService
{
    Task SaveAsync(NotificationRule rule, CancellationToken ct);
    Task UpdateAsync(NotificationRule rule, CancellationToken ct);
    Task<bool> DeleteAsync(string ruleId, CancellationToken ct);
    Task<List<NotificationRule>> FindByEntityAndTypeAsync(string entity, string type, CancellationToken ct);
    Task<List<NotificationRule>> FindByEntity(string entity, CancellationToken ct);
}

public class NotificationRuleService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<NotificationRule>(connectionFactory, CollectionName, loggerFactory), INotificationRuleService
{
    private const string CollectionName = "notificationrules";

    protected override List<CreateIndexModel<NotificationRule>> DefineIndexes(IndexKeysDefinitionBuilder<NotificationRule> builder)
    {
        var uniqueRuleIdIdx = new CreateIndexModel<NotificationRule>(builder.Ascending(r => r.RuleId),
            new CreateIndexOptions { Unique = true });
        
        var entityAndTypeIdx = new CreateIndexModel<NotificationRule>(
            builder.Combine(
                builder.Ascending(r => r.Entity),
                builder.Ascending(r => r.EventType)),
            new CreateIndexOptions { Unique = true });
        return [uniqueRuleIdIdx, entityAndTypeIdx];
    }

    public async Task SaveAsync(NotificationRule rule, CancellationToken ct)
    {
        await Collection.InsertOneAsync(rule, new InsertOneOptions(), ct);
    }

    public async Task<bool> DeleteAsync(string ruleId, CancellationToken ct)
    {
        var result = await Collection.DeleteOneAsync(r => r.RuleId == ruleId, ct);
        return result.DeletedCount > 0;
    }
    
    public async Task UpdateAsync(NotificationRule rule, CancellationToken ct)
    {
        await Collection.ReplaceOneAsync(r => r.RuleId == rule.RuleId, rule, new ReplaceOptions(), ct);
    }
    
    public async Task<List<NotificationRule>> FindByEntityAndTypeAsync(string entity, string type, CancellationToken ct)
    {
        return await Collection.Find(r => r.Entity == entity && r.EventType == type).ToListAsync(ct);
    }
    
    public async Task<List<NotificationRule>> FindByEntity(string entity, CancellationToken ct)
    {
        return await Collection.Find(r => r.Entity == entity).ToListAsync(ct);
    }
}