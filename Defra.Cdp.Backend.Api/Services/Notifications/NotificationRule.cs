using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

[BsonIgnoreExtraElements]
public record NotificationRule
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore]
    public ObjectId? MongoId { get; init; }
    
    public string RuleId { get; init; } = Guid.NewGuid().ToString();
    public required string EventType { get; init; }
    public required string Entity { get; init; } 
   
    [BsonIgnoreIfNull]
    public string? Environment { get; init; }
    
    [BsonIgnoreIfNull]
    public string? SlackChannel { get; init; } // TODO: we could either store the exact channel #foo-bar or as a ref to the team/slack, e.g. @platform:nonProd and look it up
    
    public bool IsEnabled { get; init; } = true;
}
