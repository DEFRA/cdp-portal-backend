using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public class TenantSecrets
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default;

    [property: JsonPropertyName("service")]
    public string Service { get; init; } = default!;
    
    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;
    
    [property: JsonPropertyName("secrets")]
    public List<string> Secrets { get; init; } = new();
}