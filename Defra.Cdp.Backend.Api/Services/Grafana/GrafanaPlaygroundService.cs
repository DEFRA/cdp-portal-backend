using System.Diagnostics;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPlaygroundService
{
    Task UpdatePlaygroundForService(GrafanaPlaygroundResources playgrounds, CancellationToken cancellationToken);

    Task<GrafanaPlaygroundResources?> FindPlaygroundsForService(string service, CancellationToken cancellationToken);

    Task<string> RequestUpdateForService(string service, CancellationToken cancellationToken);

    Task<GrafanaPlaygroundResources?> WaitForUpdate(string requestId, long timeoutMs, CancellationToken ct);
}

public class GrafanaPlaygroundService(IMongoDbClientFactory connectionFactory, IMonoLambdaTrigger monoLambda, ILoggerFactory loggerFactory) :
    MongoService<GrafanaPlaygroundResources>(connectionFactory, "grafanaplaygrounds", loggerFactory), IGrafanaPlaygroundService
{
    protected override List<CreateIndexModel<GrafanaPlaygroundResources>> DefineIndexes(IndexKeysDefinitionBuilder<GrafanaPlaygroundResources> builder)
    {
        var serviceIdx = builder.Ascending(g => g.Service);
        var requestIdx = builder.Ascending(g => g.RequestId);
        return [
            new CreateIndexModel<GrafanaPlaygroundResources>(serviceIdx), 
            new CreateIndexModel<GrafanaPlaygroundResources>(requestIdx)
        ];
    }

    /// <summary>
    /// Updates the playground resources for a service.
    /// </summary>
    /// <param name="playgrounds"></param>
    /// <param name="cancellationToken"></param>
    public async Task UpdatePlaygroundForService(GrafanaPlaygroundResources playgrounds, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(f => f.Service == playgrounds.Service, playgrounds,
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <summary>
    /// Finds the latest playground dashboards for a service
    /// </summary>
    /// <param name="service"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<GrafanaPlaygroundResources?> FindPlaygroundsForService(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(f => f.Service == service).FirstOrDefaultAsync(cancellationToken);
    }
    
    /// <summary>
    /// Sends a message to the MonoLambda in dev requesting it updates portal with the playground dashboards.
    /// </summary>
    /// <param name="service"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Request ID for tracking response</returns>
    public async Task<string> RequestUpdateForService(string service, CancellationToken cancellationToken)
    {
        var triggerEvent = new MonoLambdaTriggerEvent<GrafanaListPlaygroundsTrigger>
        {
            EventType = "grafana_list_playgrounds",
            Timestamp = DateTime.UtcNow,
            Payload = new GrafanaListPlaygroundsTrigger
            {
                Service = service, RequestId = Guid.NewGuid().ToString()
            }
        };
        await monoLambda.Trigger(triggerEvent, CdpEnvironments.Dev, cancellationToken);

        return triggerEvent.Payload.RequestId;
    }

    /// <summary>
    /// Queries mongo for the response for a given requestId. Retries until timeout expires
    /// </summary>
    /// <param name="requestId"></param>
    /// <param name="timeoutMs"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<GrafanaPlaygroundResources?> WaitForUpdate(string requestId, long timeoutMs, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            var data = await Collection.Find(f => f.RequestId == requestId).FirstOrDefaultAsync(ct);
        
            if (data != null)
            {
                return data;
            }
            
            try
            {
                await Task.Delay(200, ct);
            }
            catch (TaskCanceledException)
            {
                return null; 
            }
        }
        
        Logger.LogInformation("Grafana playground update for {RequestId} did not return inside of {Timeout}ms", requestId, timeoutMs);
        
        return null;
    }
}
