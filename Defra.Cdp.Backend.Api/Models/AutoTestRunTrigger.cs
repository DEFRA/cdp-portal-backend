using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;


public record AutoTestRunTrigger
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

   public DateTime Created { get; init; } = DateTime.Now;
   
   [property: JsonPropertyName("serviceName")]
   public string ServiceName { get; init; } = default!;

   [property: JsonPropertyName("environmentTestSuitesMap")]
   public Dictionary<string, List<string>?> EnvironmentTestSuitesMap { get; init; } = new();
}
