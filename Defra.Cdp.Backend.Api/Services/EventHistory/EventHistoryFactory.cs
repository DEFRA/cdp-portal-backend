using Defra.Cdp.Backend.Api.Mongo;

namespace Defra.Cdp.Backend.Api.Services.EventHistory;

public interface IEventHistoryFactory
{
    public EventHistoryRepository<T> Create<T>(long? maxDocs = null);
};

public class EventHistoryFactory(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : IEventHistoryFactory
{
    private const long DefaultMaxDocs = 500;
    private const long MaxSize = 1024 * 1024 * 200;
    
    public EventHistoryRepository<T> Create<T>(long? maxDocs)
    {
        var collection = typeof(T).Name.ToLower() + "_events";
        return new EventHistoryRepository<T>(connectionFactory, collection, loggerFactory, MaxSize, maxDocs ?? DefaultMaxDocs);
    }
}