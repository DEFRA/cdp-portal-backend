using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IVanityUrlsService : IEventsPersistenceService<VanityUrlsPayload>
{
    public Task<VanityUrlsRecord?> FindVanityUrls(string service, string environment,
        CancellationToken cancellationToken);
    
    public Task<List<VanityUrlsRecord>> FindAllVanityUrls(string service, CancellationToken cancellationToken);

}

public class VanityUrlsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<VanityUrlsRecord>(connectionFactory,
        CollectionName, loggerFactory), IVanityUrlsService
{
    private const string CollectionName = "vanityurls";
    private readonly ILogger<VanityUrlsService> _logger = loggerFactory.CreateLogger<VanityUrlsService>();

    public async Task<VanityUrlsRecord?> FindVanityUrls(string service, string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.ServiceName == service && v.Environment == environment)
            .SingleOrDefaultAsync(cancellationToken);
    }
    
    public async Task<List<VanityUrlsRecord>> FindAllVanityUrls(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.ServiceName == service)
            .ToListAsync(cancellationToken);
    }

    public async Task PersistEvent(Event<VanityUrlsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation($"Persisting vanity urls for environment: {payload.Environment}");

        var vanityUrls = payload.Services.Select(v => new VanityUrlsRecord(payload.Environment, v.Name,
                v.Urls.Select(url => new VanityUrl(url.Host, url.Domain)).Distinct().ToList()))
            .ToList();

        var vanityUrlsInDb = await FindAllVanityUrlsInEnvironment(payload.Environment, cancellationToken);


        var vanityUrlsToDelete = vanityUrlsInDb.ExceptBy(vanityUrls.Select(v => v.ServiceName),
            v => v.ServiceName).ToList();

        if (vanityUrlsToDelete.Count != 0)
        {
            await DeleteVanityUrls(vanityUrlsToDelete, cancellationToken);
        }

        var vanityUrlsInDbDict = vanityUrlsInDb.ToDictionary(v => v.ServiceName, v => v);

        var toUpdate = vanityUrls.Where(v =>
                !vanityUrlsInDbDict.ContainsKey(v.ServiceName) ||
                (vanityUrlsInDbDict.TryGetValue(v.ServiceName, out var vanityUrl) &&
                 vanityUrl.VanityUrls != v.VanityUrls))
            .ToList();

        if (toUpdate.Count != 0)
        {
            await UpdateVanityUrls(toUpdate, cancellationToken);
        }
    }

    protected override List<CreateIndexModel<VanityUrlsRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<VanityUrlsRecord> builder)
    {
        var envServiceName = new CreateIndexModel<VanityUrlsRecord>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.ServiceName)
        ));

        var env = new CreateIndexModel<VanityUrlsRecord>(
            builder.Descending(v => v.Environment)
        );
        
        var service = new CreateIndexModel<VanityUrlsRecord>(
            builder.Descending(v => v.ServiceName)
        );
        return [env, service, envServiceName];
    }

    private async Task<List<VanityUrlsRecord>> FindAllVanityUrlsInEnvironment(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    private async Task DeleteVanityUrls(List<VanityUrlsRecord> vanityUrlRecords, CancellationToken cancellationToken)
    {
        var filter = Builders<VanityUrlsRecord>.Filter.In("_id", vanityUrlRecords.Select(v => v.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task UpdateVanityUrls(List<VanityUrlsRecord> vanityUrls,
        CancellationToken cancellationToken)
    {
        var updateVanityUrlsModels =
            vanityUrls.Select(vanityUrl =>
            {
                var filterBuilder = Builders<VanityUrlsRecord>.Filter;
                var filter = filterBuilder.Where(v =>
                    v.ServiceName == vanityUrl.ServiceName && v.Environment == vanityUrl.Environment);
                return new ReplaceOneModel<VanityUrlsRecord>(filter, vanityUrl) { IsUpsert = true };
            }).ToList();

        await Collection.BulkWriteAsync(updateVanityUrlsModels, new BulkWriteOptions(), cancellationToken);
    }
}

public record VanityUrl(string Host, string Domain);

public record VanityUrlsRecord(string Environment, string ServiceName, List<VanityUrl> VanityUrls)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}