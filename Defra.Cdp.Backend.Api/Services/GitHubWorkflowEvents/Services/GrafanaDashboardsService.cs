using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IGrafanaDashboardsService : IEventsPersistenceService<GrafanaDashboardPayload>, IResourceService;

public class GrafanaDashboardsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<GrafanaDashboard>(connectionFactory,
        CollectionName, loggerFactory), IGrafanaDashboardsService
{
    private const string CollectionName = "grafanadashboards";
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public async Task PersistEvent(CommonEvent<GrafanaDashboardPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var logger = _loggerFactory.CreateLogger("GrafanaDashboardService");
        var payload = workflowEvent.Payload;
        var environment = payload.Environment;
        var entities = payload.Entities;

        logger.LogInformation("HandleGrafanaDashboard: Persisting message {Environment}", environment);

        var grafanaDashboards = entities.Select(repositoryName =>
                new GrafanaDashboard(environment, repositoryName))
            .ToList();
        await Collection.InsertManyAsync(grafanaDashboards, cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<GrafanaDashboard>> DefineIndexes(
        IndexKeysDefinitionBuilder<GrafanaDashboard> builder)
    {
        var repoNameOnly = new CreateIndexModel<GrafanaDashboard>(
            builder.Descending(v => v.RepositoryName)
        );
        var envAndRepoName = new CreateIndexModel<GrafanaDashboard>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.RepositoryName)
        ));

        return [repoNameOnly, envAndRepoName];
    }

    public string ResourceName()
    {
        return "GrafanaDashboard";
    }

    public async Task<bool> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(dashboard => dashboard.RepositoryName == repositoryName).AnyAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record GrafanaDashboard(string Environment, string RepositoryName)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}