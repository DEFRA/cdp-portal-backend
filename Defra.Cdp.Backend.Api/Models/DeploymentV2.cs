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
    public string Status { get; set; } = "";
    public bool Unstable { get; set; } = false;

    public string? ConfigVersion { get; init; } = default!;
    public TenantSecretKeys Secrets { get; set; } = new();
    
    // From ECS Service Deployment State Change messages 
    public string? LastDeploymentStatus { get; set; }
    public string? LastDeploymentMessage { get; set; }
    
    public List<TestRun> DeploymentTestRuns { get; set; } = [];

    public string? TaskDefinitionArn { get; set; }
    
    // Audit data is not returned in the API
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public Audit? Audit { get; set; }
    
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
            ConfigVersion = req.ConfigVersion
        };
    }
    
    public static DeploymentV2? FromLambdaMessage(EcsDeploymentLambdaEvent e)
    {
        var req = e.Request;
        if (req == null || e.CdpDeploymentId == null)
        {
            return null;
        }
        
        var commitSha = req.EnvFiles.Select( env => ExtractCommitSha(env.Value)).Where(sha => sha != null).FirstOrDefault("");
        
        return new DeploymentV2
        {
            CdpDeploymentId = e.CdpDeploymentId,
            Environment = req.Environment,
            Service = req.ContainerImage,
            Version = req.ContainerVersion,
            User = new UserDetails()
            {
                DisplayName = req.DeployedBy.display_name,
                Id = req.DeployedBy.user_id
            },
            Cpu = req.TaskCpu.ToString(),
            Memory = req.TaskMemory.ToString(),
            InstanceCount = req.DesiredCount,
            Created = DateTime.Now,
            Updated = DateTime.Now,
            Status = req.DesiredCount > 0 ? Requested : Undeployed,
            ConfigVersion = commitSha,
            LambdaId = e.Detail.EcsDeploymentId
        };
    }

    public static string? ExtractCommitSha(string input)
    {
        var parts = input.Split("/");
        if (parts.Length > 1 && parts[1].Length == 40) { 
            return parts[1];
        }
        return null;
    }

    // Removes the oldest stopped instance if the total instances exceeds the limit
    public void TrimInstance(int limit)
    {
        if (Instances.Count <= limit) return;

        DateTime? oldestDate = null;
        string? oldestKey = null;
        
        foreach (var (key, value) in Instances)
        {
            if (value.Status != Stopped) continue;
            if (oldestDate == null)
            {
                oldestDate = value.Updated;
                oldestKey = key;
            }
            else if(value.Updated < oldestDate)
            { 
                oldestDate = value.Updated;
                oldestKey = key;
            }
        }

        if (oldestKey != null)
        {
            Instances.Remove(oldestKey);
        }
    }
}


public class Audit
{
    public List<RepositoryTeam> ServiceOwners { get; set; } = [];
    public UserServiceUser? User { get; set; }
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