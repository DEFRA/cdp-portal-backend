using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Actions;

public interface IAppConfigVersionService
{
    Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment,
        CancellationToken cancellationToken);

    Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct);
}

public class AppConfigVersionService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfigVersion>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigVersionService
{
    private const string CollectionName = "appconfigversions";

    public async Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment,
        CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new AppConfigVersion(commitSha, commitTimestamp, environment),
            cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<AppConfigVersion>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfigVersion> builder)
    {
        return new List<CreateIndexModel<AppConfigVersion>>();
    }

    public async Task<AppConfigVersion?> FindLatestAppConfigVersion(string environment, CancellationToken ct)
    {
        return await Collection.Find(acv => acv.Environment == environment).SortByDescending(acv => acv.CommitTimestamp)
            .FirstOrDefaultAsync(ct);
    }
}

public record AppConfigVersion(string CommitSha, DateTime CommitTimestamp, string Environment)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}