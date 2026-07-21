using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;


public class GrafanaListPlaygroundsHandler(IGrafanaPlaygroundService grafanaPlaygroundService, ILogger<GrafanaListPlaygroundsHandler> logger) : IMonoLambdaEventHandler
{
    public string EventType => "grafana_list_playgrounds";

    public bool PersistEvents => false;

    public async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var response = message.Deserialize<GrafanaPlaygroundResources>();
        if (response == null)
        {
            throw new Exception("Failed to parse grafana_list_playgrounds event");
        }
        
        logger.LogInformation("Received update for {Service}'s playground dashboard, request {RequestId}", response.Service, response.RequestId);
        await grafanaPlaygroundService.UpdatePlaygroundForService(response, cancellationToken);
    }
}