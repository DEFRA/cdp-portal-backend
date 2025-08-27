using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IEnabledApisService : IEventsPersistenceService<EnabledApisPayload>
{
    Task<EnabledApiRecord> Find(string serviceName, string environment, string url,
        CancellationToken cancellationToken);
}

public class EnabledApisService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<EnabledApiRecord>(
        connectionFactory, CollectionName, loggerFactory), IEnabledApisService
{
    public const string CollectionName = "enabledapis";

    protected override List<CreateIndexModel<EnabledApiRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<EnabledApiRecord> builder)
    {
        var urlIndex = new CreateIndexModel<EnabledApiRecord>(builder.Descending(ear => ear.Api));
        var envServiceApi = new CreateIndexModel<EnabledApiRecord>(builder.Combine(
            builder.Descending(ear => ear.Environment),
            builder.Descending(ear => ear.Api),
            builder.Descending(v => v.Service)
        ));
        return [urlIndex, envServiceApi];
    }

    public async Task PersistEvent(CommonEvent<EnabledApisPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var env = workflowEvent.Payload.Environment;
        var apis = workflowEvent.Payload.Apis;

        var apisInDb = await Collection.Find(d => d.Environment == env).ToListAsync(cancellationToken);
        var toDelete = apisInDb.ExceptBy(apis.Select(a => a.Api), r => r.Api).Select(d => d.Id).ToList();

        var bulkOps = toDelete.Select(id => Builders<EnabledApiRecord>.Filter.Eq(s => s.Id, id))
            .Select(filter => new DeleteOneModel<EnabledApiRecord>(filter)).Cast<WriteModel<EnabledApiRecord>>()
            .ToList();

        bulkOps.AddRange(from api in workflowEvent.Payload.Apis
                         let filterBuilder = Builders<EnabledApiRecord>.Filter
                         let filter = filterBuilder.Eq(f => f.Api, api.Api)
                         select new ReplaceOneModel<EnabledApiRecord>(filter, new EnabledApiRecord(api.Api, env, api.Service))
                         {
                             IsUpsert = true
                         });

        if (bulkOps.Count > 0)
        {
            await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        }
    }

    public async Task<EnabledApiRecord> Find(string serviceName, string environment, string url,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(
                ear => ear.Service == serviceName && ear.Environment == environment && ear.Api == url
            )
            .FirstOrDefaultAsync(cancellationToken);
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