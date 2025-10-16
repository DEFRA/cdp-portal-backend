using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;


[BsonIgnoreExtraElements]
public class Tenant
{
    [property: JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [property: JsonPropertyName("envs")]
    public Dictionary<string, CdpTenant> Envs { get; init; } = new();
    
    [property: JsonPropertyName("metadata")]
    public TenantMetadata? Metadata { get; init; }
    
    
    // Portal data, this is not populated from the external events
    [property: JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
    
    [property: JsonPropertyName("created")]
    public DateTime? Created { get; init; }
    
    [property: JsonPropertyName("creator")]
    public UserDetails? Creator { get; init; }

    [property: JsonPropertyName("decommissioned")]
    public Decommission? Decommissioned { get; init; }
}
