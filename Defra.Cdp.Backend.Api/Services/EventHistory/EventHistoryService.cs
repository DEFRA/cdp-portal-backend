using System.Text.Json;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.EventHistory;

public interface IEventHistoryRepository<T>
{
    Task PersistEvent(string messageId, JsonElement message, CancellationToken cancellation);
}

public class EventHistoryRepository<T> : IEventHistoryRepository<T>
{

    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly ILogger _logger;
    private readonly string _source = typeof(T).Name;
    
    public EventHistoryRepository(IMongoDbClientFactory connectionFactory, string collectionName, ILoggerFactory loggerFactory, long maxSize, long? maxDocuments) 
    {
        connectionFactory.InitCappedCollection<BsonDocument>(collectionName, maxDocuments, maxSize);
        _collection = connectionFactory.GetCollection<BsonDocument>(collectionName);
        var loggerName = GetType().FullName ?? GetType().Name;
        _logger = loggerFactory.CreateLogger(loggerName);
    }

    
    public async Task PersistEvent(string messageId, JsonElement message, CancellationToken cancellation)
    {
        try
        {
            var doc = new BsonDocument
            {
                { "source", _source },
                { "messageId", messageId }, 
                { "event", BsonDocument.Parse(message.GetRawText()) }
            };
            await _collection.InsertOneAsync(doc, new InsertOneOptions(), cancellation);
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to persist event {MessageId} for {Source}: {Error}", messageId,  _source, e.Message);
        }
    }

}