using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public class FailureReason(string containerName, string reason)
{
    [property: JsonPropertyName("containerName")]
    public string ContainerName { get; set; } = containerName;

    [property: JsonPropertyName("reason")] 
    public string Reason { get; set; } = reason;

    protected bool Equals(FailureReason other)
    {
        return ContainerName == other.ContainerName && Reason == other.Reason;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((FailureReason)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ContainerName, Reason);
    }
}

public sealed class TestRun
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
    
    [property: JsonPropertyName("runId")]
    public string RunId { get; init; } = default!;
    
    [property: JsonPropertyName("testSuite")]
    public string TestSuite { get; init; } = default!;

    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;
    
    [property: JsonPropertyName("user")]
    public UserDetails User { get; init; } = default!;
    
    [property: JsonPropertyName("created")]
    public DateTime Created  { get; init; } = DateTime.Now;
    
    [property: JsonPropertyName("taskArn")]
    public string? TaskArn { get; set; }
    
    [property: JsonPropertyName("taskStatus")]    
    public string? TaskStatus { get; set; }

    [property: JsonPropertyName("taskLastUpdated")]
    public DateTime? TaskLastUpdate { get; set; }

    [property: JsonPropertyName("testStatus")]
    public string? TestStatus { get; set; }
    
    [property: JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [property: JsonPropertyName("failureReasons")]
    public List<FailureReason> FailureReasons { get; set; } = [];
    
   public static TestRun FromDeployment(DeploymentV2 deployment, string testSuite)
   {
      return new TestRun
      {
         RunId = Guid.NewGuid().ToString(),
         TestSuite = testSuite,
         Environment = deployment.Environment,
         User = deployment.User ?? null,
         Created = DateTime.Now
      };
   }

}
