using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

[BsonIgnoreExtraElements]
public class NotificationRule
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    public ObjectId? MongoId { get; init; }
    
    public string RuleId { get; init; } = Guid.NewGuid().ToString();
    
    public required string Entity { get; init; } 
    public required string EventType { get; init; }
    public Dictionary<string, string> Conditions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public string? SlackChannel { get; init; }
    public bool IsEnabled { get; init; } = true;
}
