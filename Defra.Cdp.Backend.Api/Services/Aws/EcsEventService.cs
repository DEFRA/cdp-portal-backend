using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEcsEventsService
{
    Task SaveMessage(string id, string body, DateTime messageTimestamp, CancellationToken cancellationToken);

    Task<IAsyncCursor<EcsEventCopy>> FindAll(CancellationToken cancellationToken);
}

public class EcsEventsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<EcsEventCopy>(connectionFactory,
        CollectionName, loggerFactory), IEcsEventsService
{
    private const string CollectionName = "ecsevents";

    public async Task SaveMessage(string id, string body, DateTime messageTimestamp, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new EcsEventCopy(id, messageTimestamp, body),
            cancellationToken: cancellationToken);
    }

    public async Task<IAsyncCursor<EcsEventCopy>> FindAll(CancellationToken cancellationToken)
    {
        return await Collection.Find(FilterDefinition<EcsEventCopy>.Empty).ToCursorAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<EcsEventCopy>> DefineIndexes(
        IndexKeysDefinitionBuilder<EcsEventCopy> builder)
    {
        return new List<CreateIndexModel<EcsEventCopy>>();
    }
}