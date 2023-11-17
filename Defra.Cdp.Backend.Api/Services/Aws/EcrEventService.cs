using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface IEcrEventsService
{
    Task SaveMessage(string id, string body, CancellationToken cancellationToken);
}

public class EcrEventsService : MongoService<EcrEventCopy>, IEcrEventsService
{
    private const string CollectionName = "ecrevents";

    public EcrEventsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task SaveMessage(string id, string body, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new EcrEventCopy(id, new DateTimeOffset(), body),
            cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<EcrEventCopy>> DefineIndexes(
        IndexKeysDefinitionBuilder<EcrEventCopy> builder)
    {
        var createdAtIndex = new CreateIndexModel<EcrEventCopy>(builder.Descending(r => r.CreatedAt),
            new CreateIndexOptions { Sparse = true });
        var bodyIndex = new CreateIndexModel<EcrEventCopy>(builder.Text(r => r.Body));
        return new List<CreateIndexModel<EcrEventCopy>>();
    }
}