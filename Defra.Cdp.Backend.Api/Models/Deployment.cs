using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class Deployment
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    // default condition is always ignore but it's good ot be explicit
    public ObjectId? Id { get; init; } = default!;

    public string DeploymentId { get; init; } = default!;
    public string Environment { get; init; } = default!;
    public string Service { get; init; } = default!;
    public string Version { get; init; } = default!;

    public string? User { get; init; }
    public string? UserId { get; init; }

    public DateTime DeployedAt { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string DockerImage { get; init; } = default!;
    public string? TaskId { get; init; }

    public string? EcsSvcDeploymentId { get; init; }

    public string? InstanceTaskId { get; init; }

    public int? InstanceCount { get; init; } = -1; // default value is -1 if we don't get it from legacy calls
}

public sealed class DeploymentsPage
{
    public DeploymentsPage(List<Deployment> deployments, int page, int pageSize, int totalPages)
    {
        Deployments = deployments;
        Page = page;
        PageSize = pageSize;
        TotalPages = totalPages;
    }

    public List<Deployment> Deployments { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }

    public void Deconstruct(out List<Deployment> deployments, out int page, out int pageSize, out int totalPages)
    {
        deployments = Deployments;
        page = Page;
        pageSize = PageSize;
        totalPages = TotalPages;
    }
}

public sealed class SquashedDeployment
{
    public string DeploymentId { get; init; } = default!;
    public string Environment { get; init; } = default!;
    public string Service { get; init; } = default!;
    public string Version { get; init; } = default!;

    public string? User { get; init; }
    public string? UserId { get; init; }

    public DateTime DeployedAt { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string DockerImage { get; init; } = default!;
    public string? TaskId { get; init; }

    public int Count { get; init; }
    public int? RequestedCount { get; init; } = -1; // default value is -1 if we don't get it from legacy calls
}

public sealed class SquashedDeploymentsPage
{
    public SquashedDeploymentsPage(List<SquashedDeployment> deployments, int page, int pageSize, int totalPages)
    {
        Deployments = deployments;
        Page = page;
        PageSize = pageSize;
        TotalPages = totalPages;
    }

    public List<SquashedDeployment> Deployments { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }

    public void Deconstruct(out List<SquashedDeployment> deployments, out int page, out int pageSize,
        out int totalPages)
    {
        deployments = Deployments;
        page = Page;
        pageSize = PageSize;
        totalPages = TotalPages;
    }
}