using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Terminal;

public interface ITerminalService
{
    Task CreateTerminalSession(TerminalSession session, CancellationToken cancellationToken);
}

public class TerminalService : MongoService<TerminalSession>, ITerminalService
{
    public const string CollectionName = "terminalsessions";
    public TerminalService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, CollectionName, loggerFactory)
    {
        
    }

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