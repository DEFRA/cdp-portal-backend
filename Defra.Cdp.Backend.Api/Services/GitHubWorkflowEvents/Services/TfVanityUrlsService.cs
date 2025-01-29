using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITfVanityUrlsService : IEventsPersistenceService<TfVanityUrlsPayload>;

public class TfVanityUrlsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TfVanityUrlRecord>(
        connectionFactory,
        CollectionName,
        loggerFactory), ITfVanityUrlsService
{
    public const string CollectionName = "tfvanityurls";

    protected override List<CreateIndexModel<TfVanityUrlRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<TfVanityUrlRecord> builder)
    {
        var urlIndex = new CreateIndexModel<TfVanityUrlRecord>(builder.Descending(v => v.Url));
        return [urlIndex];
    }

    public async Task PersistEvent(Event<TfVanityUrlsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var env = workflowEvent.Payload.Environment;
        var urls = workflowEvent.Payload.VanityUrls;

        var bulkOps = new List<WriteModel<TfVanityUrlRecord>>();

        var urlsInDb = await Collection.Find(d => d.Environment == env).ToListAsync(cancellationToken);
        var toDelete = urlsInDb.ExceptBy(urls.Select(u => u.PublicUrl), r => r.Url).Select(d => d.Id).ToList();

        foreach (var id in toDelete)
        {
            var filter = Builders<TfVanityUrlRecord>.Filter.Eq(s => s.Id, id);
            var deleteOne = new DeleteOneModel<TfVanityUrlRecord>(filter);
            bulkOps.Add(deleteOne);
        }

        foreach (var url in workflowEvent.Payload.VanityUrls)
        {
            var filterBuilder = Builders<TfVanityUrlRecord>.Filter;
            var filter = filterBuilder.Eq(f => f.Url, url.PublicUrl);
            var upsertOne = new ReplaceOneModel<TfVanityUrlRecord>(filter,
                new TfVanityUrlRecord(url.PublicUrl, env, url.ServiceName, url.EnableAlb, url.EnableAcm, url.IsApi))
            {
                IsUpsert = true
            };
            bulkOps.Add(upsertOne);
        }

        if (bulkOps.Count > 0)
        {
            await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        }
    }
}

[BsonIgnoreExtraElements]
public record TfVanityUrlRecord(
    string Url,
    string Environment,
    string ServiceName,
    bool EnableAlb,
    bool EnableAcm,
    bool IsApi)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}