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

    [property: JsonPropertyName("keys")] public List<string> Keys { get; init; } = new();

    [property: JsonPropertyName("lastChangedDate")]
    public string LastChangedDate { get; init; } = default!;

    [property: JsonPropertyName("createdDate")]
    public string CreatedDate { get; init; } = default!;

    public TenantSecretKeys AsTenantSecretKeys()
    {
        return new TenantSecretKeys { Keys = Keys, CreatedDate = CreatedDate, LastChangedDate = LastChangedDate };
    }
}