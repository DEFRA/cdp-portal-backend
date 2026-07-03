using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Grafana;
using MongoDB.Bson;
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
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("annotations")] public required PlaygroundAlertAnnotations Url { get; init; }
    [JsonPropertyName("created")] public required DateTime Created { get; init; }
    [JsonPropertyName("updated")] public required DateTime Updated { get; init; }
}

[BsonIgnoreExtraElements]
public record GrafanaPlaygroundResources 
{
    [JsonPropertyName("request_id")] public required string RequestId { get; init; }
    [JsonPropertyName("service")] public required string Service { get; init; }
    [JsonPropertyName("dashboards")] public required List<PlaygroundDashboard> Dashboards { get; init; }
    [JsonPropertyName("alerts")] public required List<PlaygroundAlert> Alerts { get; init; }
    [JsonPropertyName("updated")] public DateTime Updated { get; set; } = DateTime.UtcNow;
}

/*
 * {'event_type': 'grafana_list_playgrounds', 'request_id': 'dd808b65-7cb9-42db-b606-9f73a93de9ad', 'service': 'cdp-uploader', 'dashboards': [{'uid': 'd0d9cc1f-abef-44ca-be1a-ee503b737326', 'title': 'cdp-uploader (custom)', 'version': 2, 'url': '/d/d0d9cc1f-abef-44ca-be1a-ee503b737326/cdp-uploader-custom', 'created': '2026-06-18T15:21:13Z', 'updated': '2026-06-18T15:27:02Z'}, {'uid': 'a5bb51c6-ead8-4263-bb6f-b2edb18f1b4c', 'title': 'cdp-uploader (custom)', 'version': 1, 'url': '/d/a5bb51c6-ead8-4263-bb6f-b2edb18f1b4c/cdp-uploader-custom', 'created': '2026-06-05T07:45:10Z', 'updated': '2026-06-05T07:45:10Z'}], 'alerts': []}
 */
public class GrafanaListPlaygroundsHandler(IGrafanaPlaygroundService grafanaPlaygroundService, ILogger<GrafanaSnapshotHandler> logger) : IMonoLambdaEventHandler
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