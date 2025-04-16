using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Utils.Clients;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

public class DatabaseMigration
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default;

    [property: JsonPropertyName("cdpMigrationId")]
    public required string CdpMigrationId { get; init; }
    
    [property: JsonPropertyName("buildId")]
    public string? BuildId { get; init; }
    
    [property: JsonPropertyName("service")]
    public required string Service { get; init; }
    
    [property: JsonPropertyName("environment")]
    public required string Environment { get; init; }
    
    [property: JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [property: JsonPropertyName("kind")]
    public string Kind { get; init; } = "postgres-liquibase";
    
    [property: JsonPropertyName("user")]
    public required User User { get; init; }
    
    [property: JsonPropertyName("requested")]
    public DateTime Requested { get; init; } = DateTime.Now;
    
    [property: JsonPropertyName("updated")]
    public DateTime Updated { get; init; }  = DateTime.Now;
    
    [property: JsonPropertyName("status")]
    public string Status { get; init; } = "REQUESTED";
}