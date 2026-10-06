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
        
        var environmentPipeline = new EmptyPipelineDefinition<TerminalSession>()
            .Group(x => x.Environment, g => new 
            { 
                Environment = g.Key, 
                Count = g.Count() 
            });

        var environmentResult = await Collection.Aggregate(environmentPipeline, null, cancellationToken).ToListAsync(cancellationToken);
        foreach (var r in environmentResult)
        {
            metrics.RecordCount("TerminalSessionsByEnv", new Dictionary<string, string>{ {"Environment", r.Environment} } , r.Count);
        }
        
        var toolPipeline = new EmptyPipelineDefinition<TerminalSession>()
            .Group(x => x.Tool, g => new 
            { 
                Tool = g.Key,
                Count = g.Count() 
            });
        
        var toolResult = await Collection.Aggregate(toolPipeline, null, cancellationToken).ToListAsync(cancellationToken);
        foreach (var r in toolResult)
        {
            var tool = r.Tool ?? "terminal";
            // Omit the admin only images
            if(tool.EndsWith("_latest")) continue;
            metrics.RecordCount("TerminalSessionsByTool", new Dictionary<string, string>{ {"Tool", tool} } , r.Count);
        }
    }
}