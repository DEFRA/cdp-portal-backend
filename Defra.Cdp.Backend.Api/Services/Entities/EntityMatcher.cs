using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;


namespace Defra.Cdp.Backend.Api.Services.Entities;

public record EntityMatcher(
    string? Name = null,
    string? PartialName = null,
    string? TeamId = null,
    string[]? TeamIds = null,
    string? Environment = null,
    bool HasPostgres = false,
    Type? Type = null,
    Type[]? Types = null,
    SubType? SubType = null,
    Status? Status = null,
    Status[]? Statuses = null)
{
    public FilterDefinition<Entity> Match()
    {
        var builder = Builders<Entity>.Filter;
        var filter = builder.Empty;

        if (Name != null)
        {
            filter &= builder.Eq(t => t.Name, Name);
        }
        else if (PartialName != null)
        {
            filter &= builder.Regex(t => t.Name, new BsonRegularExpression(PartialName, "i"));
        }

        if (TeamId != null)
        {
            filter &= builder.AnyEq(new StringFieldDefinition<Entity>("teams.teamId"), TeamId);
        }
        else if (TeamIds is { Length: > 0 })
        {
            filter &= builder.AnyIn(new StringFieldDefinition<Entity>("teams.teamId"), TeamIds);
        }

        if (Environment != null)
        {
            filter &= builder.Exists(t => t.Envs[Environment]);
        }

        if (Type != null)
        {
            filter &= builder.Eq(t => t.Type, Type);
        }
        else if (Types is { Length: > 0})
        {
            filter &= builder.In(t => t.Type, Types);
        }

        if (SubType != null)
        {
            filter &= builder.Eq(t => t.SubType, SubType);
        }

        if (HasPostgres && Environment != null)
        {
            filter &= builder.Ne(t => t.Envs[Environment].SqlDatabase, null);
        }

        if (Status != null)
        {
            filter &= builder.Eq(t => t.Status, Status);
        }
        else if (Statuses is { Length: > 0 })
        {
            filter &= builder.In(t => t.Status, Statuses);
        }

        return filter;
    }
}