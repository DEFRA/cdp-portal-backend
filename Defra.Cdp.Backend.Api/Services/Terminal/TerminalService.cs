using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Usage;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Terminal;

public interface ITerminalService
{
    Task CreateTerminalSession(TerminalSession session, CancellationToken cancellationToken);
}

public class TerminalService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TerminalSession>(connectionFactory, CollectionName, loggerFactory), ITerminalService, IStatsReporter
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

    public async Task ReportStats(ICloudWatchMetricsService metrics, CancellationToken cancellationToken)
    {
        var totalSessions = await
            Collection.CountDocumentsAsync(FilterDefinition<TerminalSession>.Empty, null, cancellationToken);
        metrics.RecordCount("TerminalSessionsTotal", null, totalSessions);
        
        var pipeline = new EmptyPipelineDefinition<TerminalSession>()
            .Group(x => x.Environment, g => new 
            { 
                Environment = g.Key, 
                Count = g.Count() 
            });

        var result = await Collection.Aggregate(pipeline, null, cancellationToken).ToListAsync(cancellationToken);
        foreach (var r in result)
        {
            metrics.RecordCount("TerminalSessionsByEnv", new Dictionary<string, string>{ {"Environment", r.Environment} } , r.Count);
        }
    }
}