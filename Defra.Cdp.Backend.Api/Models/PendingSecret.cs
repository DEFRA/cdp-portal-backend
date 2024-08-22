using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public class PendingSecrets
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default;

    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;

    [property: JsonPropertyName("service")]
    public string Service { get; init; } = default!;

    [property: JsonPropertyName("pending")]
    public List<PendingSecret> Pending { get; init; } = [];

    [property: JsonPropertyName("exceptionMessage")]
    public List<string> ExceptionMessages { get; init; } = [];

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public class PendingSecret
{
    [property: JsonPropertyName("secretKey")]
    public string SecretKey { get; init; } = default!;

    [property: JsonPropertyName("action")] public string Action { get; init; } = default!;
}

public class RegisterPendingSecret
{
    [property: JsonPropertyName("service")]
    public string Service { get; init; } = default!;

    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;

    [property: JsonPropertyName("secretKey")]
    public string SecretKey { get; init; } = default!;

    [property: JsonPropertyName("action")] public string Action { get; init; } = default!;
}