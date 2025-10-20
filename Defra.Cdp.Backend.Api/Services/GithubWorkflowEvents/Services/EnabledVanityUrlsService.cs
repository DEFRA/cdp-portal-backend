using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

[Obsolete("Use EntityService")]
public interface IEnabledVanityUrlsService : IEventsPersistenceService<EnabledVanityUrlsPayload>;

[Obsolete("Use EntityService")]
public class EnabledVanityUrlsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<EnabledVanityUrlRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), IEnabledVanityUrlsService
{
    public const string CollectionName = "enabledvanityurls";

    protected override List<CreateIndexModel<EnabledVanityUrlRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<EnabledVanityUrlRecord> builder)
    {
        var urlIndex = new CreateIndexModel<EnabledVanityUrlRecord>(builder.Descending(v => v.Url));
        return [urlIndex];
    }

    public async Task PersistEvent(CommonEvent<EnabledVanityUrlsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var env = workflowEvent.Payload.Environment;
        var urls = workflowEvent.Payload.Urls;

        var bulkOps = new List<WriteModel<EnabledVanityUrlRecord>>();

        var urlsInDb = await Collection.Find(d => d.Environment == env).ToListAsync(cancellationToken);
        var toDelete = urlsInDb.ExceptBy(urls.Select(u => u.Url), r => r.Url).Select(d => d.Id).ToList();

        foreach (var id in toDelete)
        {
            var filter = Builders<EnabledVanityUrlRecord>.Filter.Eq(s => s.Id, id);
            var deleteOne = new DeleteOneModel<EnabledVanityUrlRecord>(filter);
            bulkOps.Add(deleteOne);
        }

        foreach (var url in workflowEvent.Payload.Urls)
        {
            var filterBuilder = Builders<EnabledVanityUrlRecord>.Filter;
            var filter = filterBuilder.Eq(f => f.Url, url.Url);
            var upsertOne = new ReplaceOneModel<EnabledVanityUrlRecord>(filter, new EnabledVanityUrlRecord(url.Url, env, url.Service)) { IsUpsert = true };
            bulkOps.Add(upsertOne);
        }

        if (bulkOps.Count > 0)
        {
            await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        }
    }
}

[BsonIgnoreExtraElements]
public record EnabledVanityUrlRecord(string Url, string Environment, string Service)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}