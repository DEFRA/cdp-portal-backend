using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

[BsonIgnoreExtraElements]
public class ResourceRequestRecord
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    public ObjectId? Id { get; init; }

    public string Status { get; init; } = "pending";
    public string EntityName { get; init; } = "";
    public List<string> Entities { get; init; } = [];
    
    public UserDetails? RequestedBy { get; init; }
    public DateTime RequestedAt { get; init; }
    public CreateTenantResourceRequest? Resources { get; init; }
    public GenericCdpWorkflowInputs? Inputs { get; init; }
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }
    public ResourceRequestPullRequest? PullRequest { get; init; }
}

public class ResourceRequestPullRequest
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("number")]
    public required int Number { get; init; }
}