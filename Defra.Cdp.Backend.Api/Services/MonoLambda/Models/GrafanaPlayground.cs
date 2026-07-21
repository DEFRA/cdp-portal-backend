using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

[BsonIgnoreExtraElements]
public record PlaygroundDashboard
{
    [JsonPropertyName("uid")] public required string Uid { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("version")] public required int Version { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("created")] public required DateTime Created { get; init; }
    [JsonPropertyName("updated")] public required DateTime Updated { get; init; }
    [JsonPropertyName("promoted")] public bool Promoted { get; init; } = false;
    
    [BsonIgnore]
    [JsonPropertyName("promotion_request")] public DashboardPromotionRequest? PromotionRequest { get; set; }

    /// <summary>
    /// Helper to enrich the model with the most recent promotion requests.
    /// </summary>
    /// <param name="requests">List of the most recent promotion request for the service</param>
    /// <returns>Updated record with a copy of the promotion request</returns>
    public PlaygroundDashboard AddPromotionRequest(List<PromotionRequestRecord> requests)
    {
        // Skip if the dashboard has already been promoted.
        if (Promoted) return this;
        
        // Only return the request if it was made after the most recent update. 
        var match = requests.Find(r =>
            r.Dashboard?.DashboardUid == Uid && r.RequestedAt > Updated);
        if (match?.Dashboard != null) PromotionRequest = match.Dashboard;
        return this;
    }
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
    
    [JsonPropertyName("alerts_promotion_request")] public AlertPromotionRequest? AlertPromotionRequest { get; set; }

    
    public GrafanaPlaygroundResources AddPromotionRequest(List<PromotionRequestRecord> requests)
    {
        foreach (var dashboard in Dashboards)
        {
            dashboard.AddPromotionRequest(requests);
        }
        AlertPromotionRequest = requests.Find(r => r.Alert != null)?.Alert;
        return this;
    }
}