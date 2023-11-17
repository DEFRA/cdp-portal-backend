using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEcsEventsService
{
    Task SaveMessage(string id, string body, CancellationToken cancellationToken);

    Task<IAsyncCursor<EcsEventCopy>> FindAll(CancellationToken cancellationToken);
}

public class EcsEventsService : MongoService<EcsEventCopy>, IEcsEventsService
{
    private const string CollectionName = "ecsevents";

    public EcsEventsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task SaveMessage(string id, string body, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new EcsEventCopy(id, new DateTime(), body),
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