using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Models;

[BsonIgnoreExtraElements]
public class PromotionRequestRecord
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    public ObjectId? Id { get; init; }
    public string? ServiceName { get; init; }
    public UserDetails? RequestedBy { get; init; }
    public DateTime RequestedAt { get; init; }
    public PromotionResource? Request { get; init; }
    public GitHubTriggerWorkflowResponse? Response { get; init; }
}