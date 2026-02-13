using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Models;

public record RepositoryTeam(string? Github, string? TeamId, string? Name);

public sealed class Repository : IEquatable<Repository>
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; init; } = null!;

    public string Description { get; init; } = null!;

    public string PrimaryLanguage { get; init; } = null!;

    public string Url { get; init; } = null!;

    public bool IsArchived { get; init; }

    public bool IsTemplate { get; init; }

    public bool IsPrivate { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<RepositoryTeam> Teams { get; init; } = null!;

    public IEnumerable<string> Topics { get; init; } = null!;

    public bool Equals(Repository? other)
    {
        return other != null &&
               Id == other.Id &&
               Description == other.Description &&
               PrimaryLanguage == other.PrimaryLanguage &&
               Url == other.Url &&
               IsArchived == other.IsArchived &&
               IsTemplate == other.IsTemplate &&
               IsPrivate == other.IsPrivate &&
               CreatedAt == other.CreatedAt &&
               Teams.ToHashSet().SetEquals(other.Teams.ToHashSet());
    }
}