using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

public interface IAppConfigVersionsService : IEventsPersistenceService<AppConfigVersionPayload>
{
    Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct);
}

public class AppConfigVersionsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfigVersion>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigVersionsService
{
    private const string CollectionName = "appconfigversions";
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public async Task PersistEvent(CommonEvent<AppConfigVersionPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var logger = _loggerFactory.CreateLogger("AppConfigVersionService");
        var payload = workflowEvent.Payload;
        var commitSha = payload.CommitSha;
        var commitTimestamp = payload.CommitTimestamp;
        var environment = payload.Environment;

        logger.LogInformation("HandleAppConfigVersion: Persisting message {CommitSha} {CommitTimestamp} {Environment}",
            commitSha, commitTimestamp, environment);
        await Collection.InsertOneAsync(new AppConfigVersion(commitSha, commitTimestamp, environment),
            cancellationToken: cancellationToken);
    }

    public async Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct)
    {
        return await Collection.Find(acv => acv.Environment == environment).SortByDescending(acv => acv.CommitTimestamp)
            .FirstOrDefaultAsync(ct);
    }

    protected override List<CreateIndexModel<AppConfigVersion>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfigVersion> builder)
    {
        return new List<CreateIndexModel<AppConfigVersion>>();
    }
}

[BsonIgnoreExtraElements]
public record AppConfigVersion(string CommitSha, DateTime CommitTimestamp, string Environment)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}