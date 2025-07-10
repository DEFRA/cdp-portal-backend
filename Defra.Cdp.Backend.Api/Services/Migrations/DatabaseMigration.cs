using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

[BsonIgnoreExtraElements]
public class DatabaseMigration
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default;

    [property: JsonPropertyName("cdpMigrationId")]
    public required string CdpMigrationId { get; init; }

    [property: JsonPropertyName("buildId")]
    public string? BuildId { get; set; }

    [property: JsonPropertyName("service")]
    public required string Service { get; init; }

    [property: JsonPropertyName("environment")]
    public required string Environment { get; init; }

    [property: JsonPropertyName("version")]
    public required string Version { get; init; }

    [property: JsonPropertyName("kind")]
    public string Kind { get; init; } = "liquibase";

    [property: JsonPropertyName("user")]
    public required UserDetails User { get; init; }

    [property: JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.Now;

    [property: JsonPropertyName("updated")]
    public DateTime Updated { get; init; } = DateTime.Now;

    [property: JsonPropertyName("status")] public string Status { get; init; } = CodeBuildStatuses.Requested;

    public static DatabaseMigration FromRequest(DatabaseMigrationRequest request)
    {
        return new DatabaseMigration
        {
            CdpMigrationId = request.CdpMigrationId,
            Service = request.Service,
            Environment = request.Environment,
            Version = request.Version,
            User = request.User
        };
    }
}

public class DatabaseMigrationRequest
{
    [property: JsonPropertyName("cdpMigrationId")]
    public required string CdpMigrationId { get; init; }

    [property: JsonPropertyName("service")]
    public required string Service { get; init; }

    [property: JsonPropertyName("environment")]
    public required string Environment { get; init; }

    [property: JsonPropertyName("version")]
    public required string Version { get; init; }

    [property: JsonPropertyName("user")]
    public required UserDetails User { get; init; }
}