using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ISquidProxyConfigService : IEventsPersistenceService<SquidProxyConfigPayload>
{
    public Task<SquidProxyConfigRecord> FindSquidProxyConfig(string service, string environment,
        CancellationToken cancellationToken);
}

public class SquidProxyConfigService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<SquidProxyConfigRecord>(connectionFactory,
        CollectionName, loggerFactory), ISquidProxyConfigService
{
    private const string CollectionName = "squidproxyconfig";
    private readonly ILogger<SquidProxyConfigService> _logger = loggerFactory.CreateLogger<SquidProxyConfigService>();

    public async Task<SquidProxyConfigRecord> FindSquidProxyConfig(string service, string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.ServiceName == service && s.Environment == environment)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task PersistEvent(Event<SquidProxyConfigPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation("Persisting squid proxy config for environment: {Environment}", payload.Environment);
        
        

        var squidProxyConfigs = payload.Services.Select(s => new SquidProxyConfigRecord(payload.Environment, s.Name,
                payload.DefaultDomains, s.AllowedDomains))
            .ToList();

        var squidProxyConfigsInDb = await FindAllSquidProxyConfigs(payload.Environment, cancellationToken);


        var squidProxyConfigsToDelete = squidProxyConfigsInDb.ExceptBy(squidProxyConfigs.Select(v => v.ServiceName),
            v => v.ServiceName).ToList();

        if (squidProxyConfigsToDelete.Count != 0)
        {
            await DeleteSquidProxyConfigs(squidProxyConfigsToDelete, cancellationToken);
        }

        var squidProxyConfigsInDbDict = squidProxyConfigsInDb.ToDictionary(v => v.ServiceName, v => v);

        var toUpdate = squidProxyConfigs.Where(v =>
                !squidProxyConfigsInDbDict.ContainsKey(v.ServiceName) ||
                (squidProxyConfigsInDbDict.TryGetValue(v.ServiceName, out var squidProxyConfig) &&
                 squidProxyConfig.DefaultDomains != v.DefaultDomains &&
                 squidProxyConfig.AllowedDomains != v.AllowedDomains)
                )
            .ToList();

        if (toUpdate.Count != 0)
        {
            await UpdateSquidProxyConfigs(toUpdate, cancellationToken);
        }
    }

    protected override List<CreateIndexModel<SquidProxyConfigRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<SquidProxyConfigRecord> builder)
    {
        var envServiceName = new CreateIndexModel<SquidProxyConfigRecord>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.ServiceName)
        ));

        return [envServiceName];
    }

    private async Task<List<SquidProxyConfigRecord>> FindAllSquidProxyConfigs(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    private async Task DeleteSquidProxyConfigs(List<SquidProxyConfigRecord> squidProxyConfigRecords, CancellationToken cancellationToken)
    {
        var filter = Builders<SquidProxyConfigRecord>.Filter.In("_id", squidProxyConfigRecords.Select(v => v.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task UpdateSquidProxyConfigs(List<SquidProxyConfigRecord> squidProxyConfigRecords,
        CancellationToken cancellationToken)
    {
        var updateSquidProxyConfigsModels =
            squidProxyConfigRecords.Select(squidProxyConfig =>
            {
                var filterBuilder = Builders<SquidProxyConfigRecord>.Filter;
                var filter = filterBuilder.Where(v =>
                    v.ServiceName == squidProxyConfig.ServiceName && v.Environment == squidProxyConfig.Environment);
                return new ReplaceOneModel<SquidProxyConfigRecord>(filter, squidProxyConfig) { IsUpsert = true };
            }).ToList();


        await Collection.BulkWriteAsync(updateSquidProxyConfigsModels, new BulkWriteOptions(), cancellationToken);
    }
}

public record SquidProxyConfigRecord(string Environment, string ServiceName, List<string> DefaultDomains, List<string> AllowedDomains)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}