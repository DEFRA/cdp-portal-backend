using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEcrEventsService
{
    Task SaveMessage(string id, string body);
}

public class EcrEventsService : MongoService<EcrEventCopy>, IEcrEventsService
{
    private const string CollectionName = "ecrevents";

    public EcrEventsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task SaveMessage(string id, string body)
    {
        await Collection.InsertOneAsync(new EcrEventCopy(id, new DateTime(), body));
    }

    protected override List<CreateIndexModel<EcrEventCopy>> DefineIndexes(
        IndexKeysDefinitionBuilder<EcrEventCopy> builder)
    {
        return new List<CreateIndexModel<EcrEventCopy>>();
    }
}