using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Models;

public record GrafanaPromotionRequest
{
     [JsonPropertyName("dashboards")]
     public List<DashboardPromotionRequest> Dashboards { get; init; } = [];
     
     [JsonPropertyName("alerts")]
     public List<AlertPromotionRequest> Alerts { get; init; } = [];
    
}

public record DashboardPromotionRequest : IGithubWorkflowInputs
{
    [JsonPropertyName("service_name")]
    [BsonElement("service_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceName { get; init; }
    
    [JsonPropertyName("dashboard_uid")]    
    [BsonElement("dashboard_uid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]    
    public string? DashboardUid { get; init; }
    
    [JsonPropertyName("dashboard_version")]
    [BsonElement("dashboard_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DashboardVersion { get; init; }

    [JsonPropertyName("environment")]
    [BsonElement("environment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PromotionEnvironment { get; init; }
}

public record AlertPromotionRequest : IGithubWorkflowInputs
{
    [JsonPropertyName("service_name")]
    [BsonElement("service_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceName { get; init; }
}