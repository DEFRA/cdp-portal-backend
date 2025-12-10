using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IAppConfigVersionsService : IGithubWorkflowEventHandler
{
    Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct);
}

public class AppConfigVersionsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfigVersion>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigVersionsService
{
    private const string CollectionName = "appconfigversions";
    private readonly ILogger _logger = loggerFactory.CreateLogger<AppConfigVersionsService>();

    public async Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct)
    {
        return await Collection.Find(acv => acv.Environment == environment).SortByDescending(acv => acv.CommitTimestamp)
            .FirstOrDefaultAsync(ct);
    }

    protected override List<CreateIndexModel<AppConfigVersion>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfigVersion> builder)
    {
        return [];
    }

    public async Task Handle(string message, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<AppConfigVersionPayload>>(message);
        if (workflowEvent == null)
        {
            _logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", message);
            return;
        }
        
        var payload = workflowEvent.Payload;
        var commitSha = payload.CommitSha;
        var commitTimestamp = payload.CommitTimestamp;
        var environment = payload.Environment;

        _logger.LogInformation("HandleAppConfigVersion: Persisting message {CommitSha} {CommitTimestamp} {Environment}",
            commitSha, commitTimestamp, environment);
        
        await Collection.InsertOneAsync(new AppConfigVersion(commitSha, commitTimestamp, environment),
            cancellationToken: cancellationToken);
    }

    public string EventType => "app-config-version";
}

[BsonIgnoreExtraElements]
public record AppConfigVersion(string CommitSha, DateTime CommitTimestamp, string Environment)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = null;
}