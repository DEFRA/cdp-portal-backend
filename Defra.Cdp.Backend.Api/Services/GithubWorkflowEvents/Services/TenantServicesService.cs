using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITenantServicesService : IEventsPersistenceService<TenantServicesPayload>, IResourceService
{
    public Task<List<TenantServiceRecord>> Find(TenantServiceFilter filter, CancellationToken cancellationToken);

    public Task<TenantServiceRecord?> FindOne(TenantServiceFilter filter, CancellationToken cancellationToken);

    public Task RefreshTeams(List<Repository> repos, CancellationToken cancellationToken);
}

public class TenantServicesService(
    IMongoDbClientFactory connectionFactory,
    IRepositoryService repositoryService,
    ILoggerFactory loggerFactory)
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

        var team = new CreateIndexModel<TenantServiceRecord>(
            builder.Descending(s => s.Teams)
        );

        return [env, service, envServiceName, team];
    }

    public async Task PersistEvent(CommonEvent<TenantServicesPayload> workflowEvent,
        CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation("Persisting tenant services for environment: {Environment}", payload.Environment);

        var teamsLookup = await repositoryService.TeamsLookup(cancellationToken);
        var tenantServices = payload.Services.Select(s => new TenantServiceRecord(payload.Environment, s.Name, s.Zone,
            s.Mongo, s.Redis, s.Postgres, s.ServiceCode, s.TestSuite, s.Buckets, s.Queues, s.ApiEnabled, s.ApiType,
            teamsLookup[s.Name].FirstOrDefault([]))
        ).ToList();

        var servicesInDb = await Find(new TenantServiceFilter { Environment = payload.Environment }, cancellationToken);

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

    public async Task<List<TenantServiceRecord>> Find(TenantServiceFilter filter, CancellationToken cancellationToken)
    {
        return await Collection.Find(filter.Filter()).ToListAsync(cancellationToken);
    }

    public async Task<TenantServiceRecord?> FindOne(TenantServiceFilter filter, CancellationToken cancellationToken)
    {
        return await Collection.Find(filter.Filter()).FirstOrDefaultAsync(cancellationToken);
    }

    public string ResourceName()
    {
        return "TenantServices";
    }

    public async Task<Boolean> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.ServiceName == repositoryName && t.Environment == "management").AnyAsync(cancellationToken);
    }
    
    public async Task RefreshTeams(List<Repository> repos, CancellationToken cancellationToken)
    {
        var updates = repos.Select(repo =>
        {
            var filterBuilder = Builders<TenantServiceRecord>.Filter;
            var filter = filterBuilder.Eq(e => e.ServiceName, repo.Id);

            var updateBuilder = Builders<TenantServiceRecord>.Update;
            var update = updateBuilder.Set(e => e.Teams, repo.Teams);
            return new UpdateManyModel<TenantServiceRecord>(filter, update) { IsUpsert = false };
        }).ToList();

        await Collection.BulkWriteAsync(updates, new BulkWriteOptions(), cancellationToken);
    }
}

public record TenantServiceFilter(
    string? Team = null,
    string? TeamId = null,
    string? Environment = null,
    string? Name = null,
    bool IsTest = false,
    bool IsService = false,
    bool HasPostgres = false)
{
    public FilterDefinition<TenantServiceRecord> Filter()
    {
        var builder = Builders<TenantServiceRecord>.Filter;
        var filter = builder.Empty;

        if (Team != null)
        {
            filter &= builder.ElemMatch(t => t.Teams, t => t.Github == Team);
        }

        if (TeamId != null)
        {
            filter &= builder.ElemMatch(t => t.Teams, t => t.TeamId == TeamId);
        }

        if (Environment != null)
        {
            filter &= builder.Eq(t => t.Environment, Environment);
        }

        if (Name != null)
        {
            filter &= builder.Eq(t => t.ServiceName, Name);
        }

        if (IsService)
        {
            filter &= builder.Eq(t => t.TestSuite, null);
        }
        else if (IsTest)
        {
            filter &= builder.Ne(t => t.TestSuite, null);
        }

        if (HasPostgres)
        {
            filter &= builder.Eq(t => t.Postgres, HasPostgres);
        }

        return filter;
    }
}

[BsonIgnoreExtraElements]
public record TenantServiceRecord(
    string Environment,
    string ServiceName,
    string Zone,
    bool Mongo,
    bool Redis,
    bool Postgres,
    string ServiceCode,
    string? TestSuite,
    List<string>? Buckets,
    List<string>? Queues,
    bool? ApiEnabled,
    string? ApiType,
    List<RepositoryTeam>? Teams
)
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
               Postgres == other.Postgres &&
               ServiceCode == other.ServiceCode &&
               TestSuite == other.TestSuite &&
               Buckets == other.Buckets &&
               Queues == (other.Queues) &&
               ApiEnabled == other.ApiEnabled &&
               ApiType == other.ApiType &&
               Teams == other.Teams;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Environment);
        hashCode.Add(ServiceName);
        hashCode.Add(Zone);
        hashCode.Add(Mongo);
        hashCode.Add(Redis);
        hashCode.Add(Postgres);
        hashCode.Add(ServiceCode);
        hashCode.Add(TestSuite);
        hashCode.Add(Buckets);
        hashCode.Add(Queues);
        hashCode.Add(ApiEnabled);
        hashCode.Add(ApiType);
        hashCode.Add(Teams);
        return hashCode.ToHashCode();
    }
}