using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Shuttering;

public interface IShutteringArchiveService
{
    public Task Archive(ShutteringRecord shutteringRecord, CancellationToken cancellationToken);
}

public class ShutteringArchiveService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<ShutteringRecord>(connectionFactory,
        CollectionName, loggerFactory), IShutteringArchiveService
{
    private const string CollectionName = "shutteringarchive";

    public async Task Archive(ShutteringRecord shutteringRecord, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(shutteringRecord, cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<ShutteringRecord>> DefineIndexes(IndexKeysDefinitionBuilder<ShutteringRecord> builder)
    {
        var entity = new CreateIndexModel<ShutteringRecord>(builder.Ascending(v => v.ServiceName));
        return [entity];
    }
}