using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Utils.Clients;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public record ShutteringRecord(
    string Environment,
    string ServiceName,
    string Url,
    string Waf,
    bool Shuttered,
    User ActionedBy,
    DateTime ActionedAt)
{
    
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
};
