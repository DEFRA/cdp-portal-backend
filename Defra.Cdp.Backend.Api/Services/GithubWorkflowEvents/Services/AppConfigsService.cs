using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IAppConfigsService : IEventsPersistenceService<AppConfigPayload>, IResourceService
{
    Task<AppConfig?> FindLatestAppConfig(string environment, string repositoryName, CancellationToken ct);
}

public class AppConfigsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfig>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigsService
{
    private const string CollectionName = "appconfigs";
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public async Task PersistEvent(CommonEvent<AppConfigPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var logger = _loggerFactory.CreateLogger("AppConfigService");
        var payload = workflowEvent.Payload;
        var commitSha = payload.CommitSha;
        var commitTimestamp = payload.CommitTimestamp;
        var environment = payload.Environment;
        var entities = payload.Entities;

        logger.LogInformation("HandleAppConfig: Persisting message {CommitSha} {CommitTimestamp} {Environment}",
            commitSha, commitTimestamp, environment);

        var filter = Builders<AppConfig>.Filter.Eq(e => e.Environment, environment);
        await Collection.DeleteManyAsync(filter, cancellationToken: cancellationToken);

        var appConfigs = entities.Select(repositoryName =>
                new AppConfig(commitSha, commitTimestamp, environment, repositoryName))
            .ToList();

        await Collection.InsertManyAsync(appConfigs, cancellationToken: cancellationToken);
    }

    public async Task<AppConfig?> FindLatestAppConfig(string environment, string repositoryName, CancellationToken ct)
    {
        return await Collection.Find(config => config.Environment == environment && config.RepositoryName == repositoryName)
            .SortByDescending(acv => acv.CommitTimestamp)
            .FirstOrDefaultAsync(ct);
    }

    protected override List<CreateIndexModel<AppConfig>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfig> builder)
    {
        var repoNameOnly = new CreateIndexModel<AppConfig>(
            builder.Descending(v => v.RepositoryName)
        );
        var envAndRepoName = new CreateIndexModel<AppConfig>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.RepositoryName)
        ));

        return [repoNameOnly, envAndRepoName];
    }

    public string ResourceName()
    {
        return "AppConfig";
    }

    public async Task<bool> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(config => config.RepositoryName == repositoryName).AnyAsync(cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record AppConfig(string CommitSha, DateTime CommitTimestamp, string Environment, string RepositoryName)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}