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

    /// <summary>
    /// Stored as raw BSON to handle multiple polymorphic resource types (s3, sqs, etc.)
    /// without requiring BSON class maps for each.
    /// </summary>
    public BsonArray Resources { get; init; } = [];

    /// <summary>
    /// The GitHub Actions run that was triggered, including run id and URLs.
    /// </summary>
    public BsonDocument? Workflow { get; init; }
}