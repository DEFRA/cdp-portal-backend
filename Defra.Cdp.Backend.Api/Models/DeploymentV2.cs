using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Models;

public class DeploymentV2
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;

    public string CdpDeploymentId { get; init; } = default!;
    public string? LambdaId { get; init; } // ID of run lambda that maps to ECS startedBy message 
    
    public string Environment { get; init; } = default!;
    public string Service { get; init; } = default!;
    public string Version { get; init; } = default!;

    public UserDetails? User { get; init; }
    public string? Cpu { get; init; }
    public string? Memory { get; init; }
    public int InstanceCount { get; init; }
    
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }

    public Dictionary<string, DeploymentInstanceStatus> Instances { get; set; } = new();
    public string Status { get; set; }
    public bool Unstable { get; set; } = false;

    public static DeploymentV2 FromRequest(RequestedDeployment req)
    {
        return new DeploymentV2
        {
            CdpDeploymentId = req.DeploymentId,
            Environment = req.Environment,
            Service = req.Service,
            Version = req.Version,
            User = req.User,
            Cpu = req.Cpu,
            Memory = req.Memory,
            InstanceCount = req.InstanceCount,
            Created = DateTime.Now,
            Updated = DateTime.Now,
            Status = req.InstanceCount > 0 ? Requested : Undeployed,
        };
    }
}

public class DeploymentInstanceStatus
{
    public string Status { get; init; }
    public DateTime Updated { get; init; }

    public DeploymentInstanceStatus(string status, DateTime updated)
    {
        Status = status;
        Updated = updated;
    }
}