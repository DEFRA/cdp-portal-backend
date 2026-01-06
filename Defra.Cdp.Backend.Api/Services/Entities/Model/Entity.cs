using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

[BsonIgnoreExtraElements]
public record Entity
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }

    [property: JsonPropertyName("name")] public required string Name { get; set; }

    [property: JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter<Type>))]
    public Type Type { get; set; }

    [property: JsonPropertyName("subType")]
    [JsonConverter(typeof(JsonStringEnumConverter<SubType>))]
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
    public Decommission? Decommissioned { get; set; }

    [property: JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [property: JsonPropertyName("environments")]
    public Dictionary<string, CdpTenant> Environments { get; init; } = new();

    [property: JsonPropertyName("metadata")]
    public TenantMetadata? Metadata { get; init; }

    [property: JsonPropertyName("progress")]
    public Dictionary<string, CreationProgress> Progress { get; init; } = new();

    [property: JsonPropertyName("overallProgress")]
    public OverallProgress? OverallProgress { get; set; }

    public void CalculateOverallProgress()
    {
        OverallProgress = new OverallProgress
        {

            IsComplete = Progress.Values
                .Where(p => p.Steps != null)
                .All(p => p.Complete),
            Steps = Progress.Values
                .Where(p => p?.Steps != null)
                .SelectMany(env => env.Steps!)
                .GroupBy(step => step.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group.All(step => step.Value)
                )
        };
    }
}

public sealed class OverallProgress
{
    [property: JsonPropertyName("isComplete")]
    public required bool IsComplete { get; init; }

    [property: JsonPropertyName("steps")]
    public required Dictionary<string, bool> Steps { get; init; }
}

public record Decommission
{
    [property: JsonPropertyName("decommissionedBy")]
    public required UserDetails DecommissionedBy { get; set; }

    [property: JsonPropertyName("started")]
    public required DateTime Started { get; set; }

    [property: JsonPropertyName("finished")]
    public required DateTime? Finished { get; set; }

    [property: JsonPropertyName("workflowsTriggered")]
    public required bool WorkflowsTriggered { get; set; }
}

[BsonIgnoreExtraElements]
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
    Prototype,
    Frontend,
    Backend,
}

public enum Status
{
    Creating,
    Created,
    Decommissioning,
    Decommissioned
}