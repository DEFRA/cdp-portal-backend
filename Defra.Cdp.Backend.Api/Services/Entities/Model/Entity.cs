using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record Entity
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }

    [property: JsonPropertyName("name")] public string Name { get; set; }

    [property: JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Type Type { get; set; }

    [property: JsonPropertyName("subType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }

    [property: JsonPropertyName("primaryLanguage")]
    public string? PrimaryLanguage { get; set; }

    [property: JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [property: JsonPropertyName("creator")]
    public UserDetails? Creator { get; set; }

    [property: JsonPropertyName("teams")] public List<Team> Teams { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [property: JsonPropertyName("status")]
    public Status Status { get; set; }

    [property: JsonPropertyName("decommissioned")]
    public Decommission Decommissioned { get; set; }
    
    [property: JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

public record Decommission
{
    [property: JsonPropertyName("decommissionedBy")]
    public required UserDetails DecommissionedBy { get; set; }

    [property: JsonPropertyName("decommissionedAt")]
    public required DateTime DecommissionedAt { get; set; }

    [property: JsonPropertyName("workflowsTriggered")]
    public required bool WorkflowsTriggered { get; set; }
}

public record Team
{
    [property: JsonPropertyName("teamId")]
    [BsonElement("teamId")]
    public string? TeamId { get; set; }

    [property: JsonPropertyName("name")] public string? Name { get; set; }
}

public enum Type
{
    Repository,
    TestSuite,
    Microservice
}

public enum SubType
{
    Journey,
    Performance,
    Frontend,
    Backend,
}

public enum Status
{
    Creating,
    Created,
    Decommissioning,
    Decommissioned,
    Failed, // TODO Remove
    Success // TODO Legacy. Do not use, use Created instead
}