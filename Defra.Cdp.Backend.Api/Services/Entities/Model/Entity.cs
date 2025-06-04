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
    public Person? Creator { get; set; }

    [property: JsonPropertyName("teams")] public List<Team> Teams { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [property: JsonPropertyName("status")]
    public Status Status { get; set; }

    [property: JsonPropertyName("decommissioned")]
    public Decommission Decommissioned { get; set; }

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

public record Decommission
{
    [property: JsonPropertyName("decommissionedBy")]
    public required Person DecommissionedBy { get; set; }

    [property: JsonPropertyName("decommissionedAt")]
    public required DateTime DecommissionedAt { get; set; }
}

public record Team
{
    [property: JsonPropertyName("teamId")]
    [BsonElement("teamId")]
    public string? TeamId { get; set; }

    [property: JsonPropertyName("name")] public string? Name { get; set; }
}

public record Person
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
    Creating,
    Created,
    Failed, // TODO Remove
    Success // TODO Legacy. Do not use, use Created instead
}

public static class StatusExtensions
{
    public static Status ToStatus(this string status)
    {
        switch (status)
        {
            case "in-progress": return Status.Creating;
            case "success": return Status.Created;
            case "failed": return Status.Failed;
            default: throw new ArgumentOutOfRangeException();
        }
    }

}