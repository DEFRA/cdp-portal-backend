using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public record AutoTestRunTriggerDto
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }

    public DateTime Created { get; init; } = DateTime.Now;

    [property: JsonPropertyName("serviceName")]
    public string ServiceName { get; init; } = null!;

    [property: JsonPropertyName("profile")]
    public string Profile { get; init; } = "";

    [property: JsonPropertyName("testSuite")]
    public string TestSuite { get; init; } = null!;

    [property: JsonPropertyName("environments")]
    public List<string> Environments { get; init; } = [];
}