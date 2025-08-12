using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITenantRdsDatabasesService  : IEventsPersistenceService<TenantDatabasePayload>
{
    public Task<List<TenantRdsDatabase>> Find(string? service, string? environment, CancellationToken cancellationToken);
    public Task<List<TenantRdsDatabase>> Find(string? service, CancellationToken cancellationToken);
}

public class TenantRdsDatabasesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TenantRdsDatabase>(connectionFactory, CollectionName, loggerFactory), ITenantRdsDatabasesService 
{
    public static readonly string CollectionName = "tenant-rds-databases";

    public async Task PersistEvent(CommonEvent<TenantDatabasePayload> workflowEvent, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var environment = workflowEvent.Payload.Environment;
        var databases = workflowEvent.Payload.RdsDatabases
            .Where(d => d.Service != null)
            .Select(d => new TenantRdsDatabase(
                null,
                Service: d.Service!,
                Environment: environment,
                DatabaseName: d.DatabaseName,
                Endpoint: d.Endpoint,
                ReaderEndpoint: d.ReaderEndpoint,
                Engine: d.Engine,
                EngineVersion: d.EngineVersion,
                Port: d.Port,
                BackupRetentionPeriod : d.BackupRetentionPeriod,
                EarliestRestorableTime : d.EarliestRestorableTime,
                LatestRestorableTime : d.LatestRestorableTime,
                Updated: now));
        
        await Collection.InsertManyAsync(databases, new InsertManyOptions(), cancellationToken);
        
        
        var fb = new FilterDefinitionBuilder<TenantRdsDatabase>();
        var filter = fb.And(
            fb.Eq(d => d.Environment, environment),
            fb.Lt(d => d.Updated, now));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    public async Task<List<TenantRdsDatabase>> Find(string? service, string? environment, CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<TenantRdsDatabase>();
        var filter = fb.Empty;
        if (service != null)
        {
            filter &= fb.Eq(d => d.Service, service);
        }
        if (environment != null)
        {
            filter &= fb.Eq(d => d.Environment, environment);
        }

        return await Collection.Find(filter).ToListAsync(cancellationToken);

    }

    public Task<List<TenantRdsDatabase>> Find(string? service, CancellationToken cancellationToken)
    {
        return Find(service, null, cancellationToken);
    }

    protected override List<CreateIndexModel<TenantRdsDatabase>> DefineIndexes(IndexKeysDefinitionBuilder<TenantRdsDatabase> builder)
    {
        return [
            new CreateIndexModel<TenantRdsDatabase>( builder.Ascending(d => d.Service)),
            new CreateIndexModel<TenantRdsDatabase>(builder.Ascending(d => d.Environment))
        ];
    }
}

public record TenantRdsDatabase(
    [property: BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: BsonIgnoreIfDefault]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    ObjectId? Id,
    string Service, 
    string Environment, 
    string DatabaseName,
    string Endpoint,
    string ReaderEndpoint,
    int Port,
    string? Engine,
    string? EngineVersion,
    DateTime? EarliestRestorableTime,
    DateTime? LatestRestorableTime,
    int BackupRetentionPeriod,
    DateTime Updated);