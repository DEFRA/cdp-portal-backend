using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Grafana;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;

[BsonIgnoreExtraElements]
public record PlaygroundDashboard
{
    [JsonPropertyName("uid")] public required string Uid { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("version")] public required int Version { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("created")] public required string Created { get; init; }
    [JsonPropertyName("updated")] public required string Updated { get; init; }
    [JsonPropertyName("promoted")] public bool Promoted { get; init; } = false;
}

[BsonIgnoreExtraElements]
public record PlaygroundAlertAnnotations
{
    [property: JsonPropertyName("description")]
    public string? Description { get; set; }

    [property: JsonPropertyName("summary")]
    public string? Summary { get; set; }
}

[BsonIgnoreExtraElements]
public record PlaygroundAlert
{
    [JsonPropertyName("uid")] public required string Uid { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = "custom";
    [JsonPropertyName("annotations")] public PlaygroundAlertAnnotations Url { get; init; } = new();
}

[BsonIgnoreExtraElements]
public record GrafanaPlaygroundResources 
{
    [JsonPropertyName("request_id")] public required string RequestId { get; init; }
    [JsonPropertyName("service")] public required string Service { get; init; }
    [JsonPropertyName("dashboards")] public List<PlaygroundDashboard> Dashboards { get; init; } = [];
    [JsonPropertyName("alerts")] public List<PlaygroundAlert> Alerts { get; init; } = [];
    [JsonPropertyName("updated")] public DateTime Updated { get; set; } = DateTime.UtcNow;
}

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