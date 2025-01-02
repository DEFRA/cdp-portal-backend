using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITenantServicesService : IEventsPersistenceService<TenantServicesPayload>
{
    public Task<TenantServiceRecord?> FindService(string service, string environment,
        CancellationToken cancellationToken);

    public Task<List<TenantServiceRecord>> FindAllServices(string service, CancellationToken cancellationToken);
}

public class TenantServicesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TenantServiceRecord>(connectionFactory,
        CollectionName, loggerFactory), ITenantServicesService
{
    private const string CollectionName = "tenantservices";
    private readonly ILogger<TenantServicesService> _logger = loggerFactory.CreateLogger<TenantServicesService>();

    protected override List<CreateIndexModel<TenantServiceRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<TenantServiceRecord> builder)
    {
        var envServiceName = new CreateIndexModel<TenantServiceRecord>(builder.Combine(
            builder.Descending(s => s.Environment),
            builder.Descending(s => s.ServiceName)
        ));

        var env = new CreateIndexModel<TenantServiceRecord>(
            builder.Descending(s => s.Environment)
        );

        var service = new CreateIndexModel<TenantServiceRecord>(
            builder.Descending(s => s.ServiceName)
        );
        return [env, service, envServiceName];
    }

    public async Task PersistEvent(Event<TenantServicesPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation($"Persisting tenant services for environment: {payload.Environment}");

        var tenantServices = payload.Services.Select(s => new TenantServiceRecord(payload.Environment, s.Name, s.Zone,
            s.Mongo, s.Redis, s.ServiceCode, s.TestSuite, s.Buckets, s.Queues)).ToList();

        var servicesInDb = await FindAllServicesInEnvironment(payload.Environment, cancellationToken);

        var servicesToDelete = servicesInDb.ExceptBy(tenantServices.Select(s => s.ServiceName),
            s => s.ServiceName).ToList();

        if (servicesToDelete.Count != 0)
        {
            await DeleteServices(servicesToDelete, cancellationToken);
        }

        var servicesInDbDict = servicesInDb.ToDictionary(s => s.ServiceName, s => s);

        var toUpdate = tenantServices.Where(s =>
                !servicesInDbDict.ContainsKey(s.ServiceName) ||
                (servicesInDbDict.TryGetValue(s.ServiceName, out var service) &&
                 !service.Equals(s)))
            .ToList();

        if (toUpdate.Count != 0)
        {
            await UpdateServices(toUpdate, cancellationToken);
        }
    }

    private async Task UpdateServices(List<TenantServiceRecord> tenantServices,
        CancellationToken cancellationToken)
    {
        var updateServicesModels =
            tenantServices.Select(service =>
            {
                var filterBuilder = Builders<TenantServiceRecord>.Filter;
                var filter = filterBuilder.Where(s =>
                    s.ServiceName == service.ServiceName && s.Environment == service.Environment);
                return new ReplaceOneModel<TenantServiceRecord>(filter, service) { IsUpsert = true };
            }).ToList();

        await Collection.BulkWriteAsync(updateServicesModels, new BulkWriteOptions(), cancellationToken);
    }

    private async Task DeleteServices(List<TenantServiceRecord> tenantServiceRecords,
        CancellationToken cancellationToken)
    {
        var filter = Builders<TenantServiceRecord>.Filter.In("_id", tenantServiceRecords.Select(s => s.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task<List<TenantServiceRecord>> FindAllServicesInEnvironment(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantServiceRecord?> FindService(string service, string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.ServiceName == service && s.Environment == environment)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<List<TenantServiceRecord>> FindAllServices(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.ServiceName == service)
            .ToListAsync(cancellationToken);
    }
}

public record TenantServiceRecord(
    string Environment,
    string ServiceName,
    string Zone,
    bool Mongo,
    bool Redis,
    string ServiceCode,
    string? TestSuite,
    List<string>? Buckets,
    List<string>? Queues)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
    
    public virtual bool Equals(TenantServiceRecord? other)
    {
        return Environment == other?.Environment &&
               ServiceName == other.ServiceName &&
               Zone == other.Zone &&
               Mongo == other.Mongo &&
               Redis == other.Redis &&
               ServiceCode == other.ServiceCode &&
               TestSuite == other.TestSuite &&
               Buckets == other.Buckets &&
               Queues == (other.Queues);
    }
}