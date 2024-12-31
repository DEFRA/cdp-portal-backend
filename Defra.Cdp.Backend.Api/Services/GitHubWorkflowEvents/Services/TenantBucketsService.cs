using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITenantBucketsService : IEventsPersistenceService<TenantBucketsPayload>
{
    public Task<List<TenantBucketRecord>> FindBuckets(string service, string environment,
        CancellationToken cancellationToken);

    public Task<List<TenantBucketRecord>> FindAllBuckets(string service, CancellationToken cancellationToken);
}

public class TenantBucketsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TenantBucketRecord>(connectionFactory,
        CollectionName, loggerFactory), ITenantBucketsService
{
    private const string CollectionName = "tenantbuckets";
    private readonly ILogger<TenantBucketsService> _logger = loggerFactory.CreateLogger<TenantBucketsService>();

    protected override List<CreateIndexModel<TenantBucketRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<TenantBucketRecord> builder)
    {
        var envServiceName = new CreateIndexModel<TenantBucketRecord>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.ServiceName)
        ));

        var env = new CreateIndexModel<TenantBucketRecord>(
            builder.Descending(v => v.Environment)
        );

        var service = new CreateIndexModel<TenantBucketRecord>(
            builder.Descending(v => v.ServiceName)
        );
        return [env, service, envServiceName];
    }

    public async Task PersistEvent(Event<TenantBucketsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation($"Persisting tenant buckets for environment: {payload.Environment}");


        var tenantBucketRecords = payload.Buckets.SelectMany(bucket =>
        {
            return bucket.ServicesWithAccess.Select(serviceName =>
                new TenantBucketRecord(payload.Environment, serviceName, bucket.Name)).ToList();
        }).ToList();
        
        
        var servicesInDb = await FindAllBucketsInEnvironment(payload.Environment, cancellationToken);
        

        var servicesToDelete = servicesInDb.ExceptBy(tenantBucketRecords.Select(s => s.ToString()),
            s => s.ToString()).ToList();
        
        if (servicesToDelete.Count != 0)
        {
            await DeleteBuckets(servicesToDelete, cancellationToken);
        }

        var servicesInDbDict = servicesInDb.ToDictionary(s => s.ToString(), s => s);

        var toUpdate = tenantBucketRecords.Where(s =>
                !servicesInDbDict.ContainsKey(s.ToString()) ||
                (servicesInDbDict.TryGetValue(s.ToString(), out var service) &&
                 !service.Equals(s)))
            .ToList();

        if (toUpdate.Count != 0)
        {
            await UpdateBuckets(toUpdate, cancellationToken);
        }
    }

    public async Task<List<TenantBucketRecord>> FindBuckets(string service, string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.ServiceName == service && s.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TenantBucketRecord>> FindAllBuckets(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.ServiceName == service)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<TenantBucketRecord>> FindAllBucketsInEnvironment(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    private async Task UpdateBuckets(List<TenantBucketRecord> tenantBuckets,
        CancellationToken cancellationToken)
    {
        var updateServicesModels =
            tenantBuckets.Select(service =>
            {
                var filterBuilder = Builders<TenantBucketRecord>.Filter;
                var filter = filterBuilder.Where(s =>
                    s.ServiceName == service.ServiceName && s.Environment == service.Environment && s.Bucket == service.Bucket);
                return new ReplaceOneModel<TenantBucketRecord>(filter, service) { IsUpsert = true };
            }).ToList();

        await Collection.BulkWriteAsync(updateServicesModels, new BulkWriteOptions(), cancellationToken);
    }


    private async Task DeleteBuckets(List<TenantBucketRecord> tenantBucketRecords,
        CancellationToken cancellationToken)
    {
        var filter = Builders<TenantBucketRecord>.Filter.In("_id", tenantBucketRecords.Select(s => s.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }
}

public record TenantBucketRecord(
    string Environment,
    string ServiceName,
    string Bucket)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;

    public virtual bool Equals(TenantBucketRecord? other)
    {
        return Environment == other.Environment && ServiceName == other.ServiceName && Bucket == other.Bucket;
    }
    
    public override int GetHashCode() => HashCode.Combine(Environment, ServiceName, Bucket);

    public override string ToString()
    {
        return $"{{Environment = {Environment}, ServiceName = {ServiceName}, Bucket = {Bucket}}}";
    }
}