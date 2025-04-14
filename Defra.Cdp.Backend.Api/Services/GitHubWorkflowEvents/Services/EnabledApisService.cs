using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IEnabledApisService : IEventsPersistenceService<EnabledApisPayload>;

public class EnabledApisService (IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<EnabledApiRecord>(
    connectionFactory,
    CollectionName, 
    loggerFactory), IEnabledApisService
{
    public const string CollectionName = "enabledapis";

    protected override List<CreateIndexModel<EnabledApiRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<EnabledApiRecord> builder)
    {
        var urlIndex = new CreateIndexModel<EnabledApiRecord>(builder.Descending(v => v.Api));
        return [urlIndex];
    }

    public async Task PersistEvent(CommonEvent<EnabledApisPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var env = workflowEvent.Payload.Environment;
        var apis = workflowEvent.Payload.Apis;
        
        var bulkOps = new List<WriteModel<EnabledApiRecord>>();
        
        var apisInDb = await Collection.Find(d => d.Environment == env).ToListAsync(cancellationToken);
        var toDelete = apisInDb.ExceptBy(apis.Select(a => a.Api), r => r.Api).Select(d => d.Id).ToList();
        
        foreach (var id in toDelete)
        {
            var filter = Builders<EnabledApiRecord>.Filter.Eq(s => s.Id, id);
            var deleteOne = new DeleteOneModel<EnabledApiRecord>(filter);
            bulkOps.Add(deleteOne);
        }
        
        foreach (var api in workflowEvent.Payload.Apis)
        {
            var filterBuilder = Builders<EnabledApiRecord>.Filter;
            var filter = filterBuilder.Eq(f => f.Api, api.Api);
            var upsertOne = new ReplaceOneModel<EnabledApiRecord>(filter, new EnabledApiRecord(api.Api, env, api.Service)) { IsUpsert = true };
            bulkOps.Add(upsertOne);
        }
        
        if (bulkOps.Count > 0)
        {
            await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        }
    }
}

[BsonIgnoreExtraElements]
public record EnabledApiRecord(string Api, string Environment, string Service)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}