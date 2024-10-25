using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Actions;

public interface IAppConfigEventService
{
    Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment, CancellationToken cancellationToken);

}

public class AppConfigEventService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfigEvent>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigEventService
{
    private const string CollectionName = "appconfigevents";
    
    public async Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new AppConfigEvent(commitSha, commitTimestamp, environment),
            cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<AppConfigEvent>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfigEvent> builder)
    {
        return new List<CreateIndexModel<AppConfigEvent>>();
    }
}

public sealed record AppConfigEvent(
    string CommitSha,
    DateTime CommitTimestamp,
    string Environment
);