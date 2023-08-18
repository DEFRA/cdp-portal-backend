using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEcsEventsService
{
    Task SaveMessage(string id, string body);

    Task<IAsyncCursor<EcsEventCopy>> FindAll();
}

public class EcsEcsEventsService : MongoService<EcsEventCopy>, IEcsEventsService
{
    private const string CollectionName = "ecsevents";

    public EcsEcsEventsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task SaveMessage(string id, string body)
    {
        await Collection.InsertOneAsync(new EcsEventCopy(id, new DateTime(), body));
    }

    public async Task<IAsyncCursor<EcsEventCopy>> FindAll()
    {
        return await Collection.Find(FilterDefinition<EcsEventCopy>.Empty).ToCursorAsync();
    }

    protected override List<CreateIndexModel<EcsEventCopy>> DefineIndexes(
        IndexKeysDefinitionBuilder<EcsEventCopy> builder)
    {
        return new List<CreateIndexModel<EcsEventCopy>>();
    }
}