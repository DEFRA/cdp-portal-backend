using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Actions;

public interface IAppConfigVersionService
{
    Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment, CancellationToken cancellationToken);

}

public class AppConfigVersionService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AppConfigVersion>(connectionFactory,
        CollectionName, loggerFactory), IAppConfigVersionService
{
    private const string CollectionName = "appconfigversions";
    
    public async Task SaveMessage(string commitSha, DateTime commitTimestamp, string environment, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new AppConfigVersion(commitSha, commitTimestamp, environment),
            cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<AppConfigVersion>> DefineIndexes(
        IndexKeysDefinitionBuilder<AppConfigVersion> builder)
    {
        return new List<CreateIndexModel<AppConfigVersion>>();
    }
}

public sealed record AppConfigVersion(
    string CommitSha,
    DateTime CommitTimestamp,
    string Environment
);