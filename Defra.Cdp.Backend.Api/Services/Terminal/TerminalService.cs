using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Terminal;

public interface ITerminalService
{
    Task CreateTerminalSession(TerminalSession session, CancellationToken cancellationToken);
}

public class TerminalService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TerminalSession>(connectionFactory, CollectionName, loggerFactory), ITerminalService
{
    public const string CollectionName = "terminalsessions";

    protected override List<CreateIndexModel<TerminalSession>> DefineIndexes(IndexKeysDefinitionBuilder<TerminalSession> builder)
    {
        var index = new CreateIndexModel<TerminalSession>(builder.Combine(
            builder.Ascending(t => t.Environment),
            builder.Ascending(t => t.Service),
            builder.Descending(t => t.Requested)
        ));

        return [index];
    }

    public async Task CreateTerminalSession(TerminalSession session, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(session, new InsertOneOptions(), cancellationToken);
    }
}