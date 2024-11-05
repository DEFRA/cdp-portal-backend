using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;


public record DeploymentTrigger
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

   public DateTime Created { get; init; } = DateTime.Now;

   [property: JsonPropertyName("repository")]
   public string Repository { get; init; } = default!;

   [property: JsonPropertyName("testSuite")]
   public string TestSuite { get; init; } = default!;

   [property: JsonPropertyName("environments")]
   public List<string> Environments { get; init; } = new();

}
