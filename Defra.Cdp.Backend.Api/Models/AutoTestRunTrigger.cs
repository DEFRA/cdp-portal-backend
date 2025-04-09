using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public record AutoTestRunTrigger
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = null!;

    public DateTime Created { get; init; } = DateTime.Now;

    [property: JsonPropertyName("serviceName")]
    public string ServiceName { get; init; } = null!;

    [property: JsonPropertyName("testSuites")]
    public Dictionary<string, List<string>> TestSuites { get; init; } = new();

    [property: JsonPropertyName("testSuite")]
    public string? TestSuite { get; init; } = null!;

    [property: JsonPropertyName("environments")]
    public List<string> Environments { get; init; } = new();
}