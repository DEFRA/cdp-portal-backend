using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IShutteredUrlsService : IEventsPersistenceService<ShutteredUrlsPayload>
{
    public Task<List<ShutteredUrlRecord>> FindShutteredUrls(string environment, CancellationToken cancellationToken);
    public Task<List<ShutteredUrlRecord>> FindShutteredUrls(CancellationToken cancellationToken);
}

public class ShutteredUrlsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<ShutteredUrlRecord>(
        connectionFactory,
        CollectionName,
        loggerFactory), IShutteredUrlsService
{
    public const string CollectionName = "shutteredurls";

    protected override List<CreateIndexModel<ShutteredUrlRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ShutteredUrlRecord> builder)
    {
        var urlIndex = new CreateIndexModel<ShutteredUrlRecord>(builder.Descending(v => v.Url));
        return [urlIndex];
    }

    public async Task PersistEvent(CommonEvent<ShutteredUrlsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var env = workflowEvent.Payload.Environment;
        var urls = workflowEvent.Payload.Urls;

        var urlsInDb = await Collection.Find(d => d.Environment == env).ToListAsync(cancellationToken);
        var toDelete = urlsInDb.ExceptBy(urls, record => record.Url).Select(r => r.Id).ToList();

        var bulkOps = toDelete.Select(id => Builders<ShutteredUrlRecord>.Filter.Eq(s => s.Id, id))
            .Select(filter => new DeleteOneModel<ShutteredUrlRecord>(filter)).Cast<WriteModel<ShutteredUrlRecord>>()
            .ToList();

        bulkOps.AddRange(from url in workflowEvent.Payload.Urls
                         let filterBuilder = Builders<ShutteredUrlRecord>.Filter
                         let filter = filterBuilder.And(filterBuilder.Eq(s => s.Environment, env),
                             filterBuilder.Eq(s => s.Url, url))
                         let update = Builders<ShutteredUrlRecord>.Update.Set(s => s.Url, url)
                         select new UpdateOneModel<ShutteredUrlRecord>(filter, update) { IsUpsert = true });

        if (bulkOps.Count > 0)
        {
            await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        }
    }

    public async Task<List<ShutteredUrlRecord>> FindShutteredUrls(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.Environment == environment).ToListAsync(cancellationToken);
    }

    public async Task<List<ShutteredUrlRecord>> FindShutteredUrls(CancellationToken cancellationToken)
    {
        return await Collection.Find(FilterDefinition<ShutteredUrlRecord>.Empty).ToListAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record ShutteredUrlRecord(string Environment, string Url)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}