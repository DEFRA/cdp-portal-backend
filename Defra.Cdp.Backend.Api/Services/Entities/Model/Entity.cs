using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record Entity
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }

    [property: JsonPropertyName("name")] public string Name { get; set; }

    [property: JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Type Type { get; set; }

    [property: JsonPropertyName("subType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }

    [property: JsonPropertyName("primaryLanguage")]
    public string? PrimaryLanguage { get; set; }

    [property: JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [property: JsonPropertyName("creator")]
    public Creator? Creator { get; set; }

    [property: JsonPropertyName("teams")] public List<Team> Teams { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [property: JsonPropertyName("status")]
    public Status Status { get; set; }

    public static Entity from(LegacyStatus status)
    {
        Type? type = null;
        SubType? subType = null;

        switch (status.Kind)
        {
            case "journey-testsuite":
                type = Type.TestSuite;
                subType = Model.SubType.Journey;
                break;
            case "perf-testsuite":
                type = Type.TestSuite;
                subType = Model.SubType.Performance;
                break;
            case "repository":
                type = Type.Repository;
                break;
            case "microservice":
                type = Type.Microservice;
                switch (status.Zone.ToLower())
                {
                    case "public":
                        subType = Model.SubType.Frontend;
                        break;
                    case "protected":
                        subType = Model.SubType.Backend;
                        break;
                }

                break;
        }

        return new Entity
        {
            Name = status.RepositoryName,
            Type = type ?? throw new ArgumentOutOfRangeException(nameof(status.Kind), status.Kind, null),
            SubType = subType,
            Created = status.Started,
            Creator = status.Creator.toCreator(),
            Teams = [status.Team.toTeam()],
            Status = status.Status.ToStatus()
        };
    }
}

public class Team
{
    [property: JsonPropertyName("teamId")]
    [BsonElement("teamId")]
    public string? TeamId { get; set; }

    [property: JsonPropertyName("name")] public string? Name { get; set; }
}

public class Creator
{
    [property: JsonPropertyName("id")] public string? Id { get; set; }

    [property: JsonPropertyName("displayName")]
    public string? Name { get; set; }
}

public enum Type
{
    Repository,
    TestSuite,
    Microservice
}

public enum SubType
{
    Journey,
    Performance,
    Frontend,
    Backend,
}

public enum Status
{
    InProgress,
    Success,
    Failed
}

public static class StatusExtensions
{
    public static Status ToStatus(this string status)
    {
        switch (status)
        {
            case "in-progress": return Status.InProgress;
            case "success": return Status.Success;
            case "failed": return Status.Failed;
            default: throw new ArgumentOutOfRangeException();
        }
    }
    
}