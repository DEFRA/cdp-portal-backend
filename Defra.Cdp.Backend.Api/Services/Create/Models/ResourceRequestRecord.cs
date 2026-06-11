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
    
    public string EntityName { get; init; } = "";
    public UserDetails? RequestedBy { get; init; }
    public DateTime RequestedAt { get; init; }
    public CreateTenantResourceRequest? Resources { get; init; }
    public GenericCdpWorkflowInputs? Inputs { get; init; }
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }
}