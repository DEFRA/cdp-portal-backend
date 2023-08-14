using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Repositories.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEventsService
{
    Task SaveMessage(string id, string body);

    Task<IAsyncCursor<EcsEventCopy>> FindAll();
}

public class EventsService : IEventsService
{
    private readonly IMongoCollection<EcsEventCopy> _ecsEventsCollection;

    public EventsService(IMongoDbClientFactory connectionFactory)
    {
        _ecsEventsCollection = connectionFactory.GetCollection<EcsEventCopy>("ecsevents");
    }

    public async Task SaveMessage(string id, string body)
    {
        await _ecsEventsCollection.InsertOneAsync(new EcsEventCopy(id, new DateTime(), body));
    }

    public async Task<IAsyncCursor<EcsEventCopy>> FindAll()
    {
        return await _ecsEventsCollection.Find(FilterDefinition<EcsEventCopy>.Empty).ToCursorAsync();
    }
}