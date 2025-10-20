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
public interface INginxUpstreamsService : IEventsPersistenceService<NginxUpstreamsPayload>, IResourceService;

public class NginxUpstreamsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<NginxUpstreamsRecord>(connectionFactory,
        CollectionName, loggerFactory), INginxUpstreamsService
{
    private const string CollectionName = "nginxupstreams";

    private readonly ILogger<NginxUpstreamsService> _logger = loggerFactory.CreateLogger<NginxUpstreamsService>();

    public async Task PersistEvent(CommonEvent<NginxUpstreamsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation("Persisting nginx upstreams for environment: {Environment}", payload.Environment);

        var upstreams = payload.Entities.Select(repository => new NginxUpstreamsRecord(payload.Environment, repository)).ToList();

        var upstreamsInDb = await FindAllEnvironmentUpstreams(payload.Environment, cancellationToken);

        var upstreamsToDelete = upstreamsInDb.ExceptBy(upstreams.Select(v => v.RepositoryName),
            v => v.RepositoryName).ToList();

        if (upstreamsToDelete.Count != 0)
        {
            await DeleteUpstreams(upstreamsToDelete, cancellationToken);
        }

        if (upstreams.Count != 0)
        {
            await UpdateUpstreams(upstreams, cancellationToken);
        }
    }

    protected override List<CreateIndexModel<NginxUpstreamsRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<NginxUpstreamsRecord> builder)
    {
        var envRepoName = new CreateIndexModel<NginxUpstreamsRecord>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.RepositoryName)
        ));

        var env = new CreateIndexModel<NginxUpstreamsRecord>(
            builder.Descending(v => v.Environment)
        );

        var repoName = new CreateIndexModel<NginxUpstreamsRecord>(
            builder.Descending(v => v.RepositoryName)
        );
        return [env, repoName, envRepoName];
    }

    private async Task<List<NginxUpstreamsRecord>> FindAllEnvironmentUpstreams(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    private async Task DeleteUpstreams(List<NginxUpstreamsRecord> upstreamRecords, CancellationToken cancellationToken)
    {
        var filter = Builders<NginxUpstreamsRecord>.Filter.In("_id", upstreamRecords.Select(v => v.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task UpdateUpstreams(List<NginxUpstreamsRecord> upstreams,
        CancellationToken cancellationToken)
    {
        var updateUpstreamsModels =
            upstreams.Select(upstream =>
            {
                var filterBuilder = Builders<NginxUpstreamsRecord>.Filter;
                var filter = filterBuilder.Where(v =>
                    v.RepositoryName == upstream.RepositoryName && v.Environment == upstream.Environment);
                return new ReplaceOneModel<NginxUpstreamsRecord>(filter, upstream) { IsUpsert = true };
            }).ToList();

        await Collection.BulkWriteAsync(updateUpstreamsModels, new BulkWriteOptions(), cancellationToken);
    }

    public string ResourceName()
    {
        return "NginxUpstreams";
    }

    public async Task<bool> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.RepositoryName == repositoryName).AnyAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record NginxUpstreamsRecord(string Environment, string RepositoryName)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}