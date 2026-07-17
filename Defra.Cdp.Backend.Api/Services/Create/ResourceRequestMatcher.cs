using Defra.Cdp.Backend.Api.Services.Create.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public record ResourceRequestMatcher(
    string[]? Name,
    string[]? TeamIds,
    string[]? Status,
    string? UserId,
    DateTime? ModifiedAfter)
{
    public FilterDefinition<ResourceRequestRecord> Match()
    {
        var builder = Builders<ResourceRequestRecord>.Filter;
        var filter = builder.Empty;

        
        if (Name is { Length: > 0 })
        {
            filter &= builder.AnyIn(r => r.Entities, Name);
        }
        
        if (TeamIds is { Length: > 0 })
        {
            filter &= builder.AnyIn(new StringFieldDefinition<ResourceRequestRecord>("teams.teamId"), TeamIds);
        }
        
        if (Status is { Length: > 0 })
        {
            filter &= builder.In(r => r.Status, Status);
        }

        if (UserId != null)
        {
            filter &= builder.Eq(r => r.RequestedBy!.Id, UserId);
        }

        if (ModifiedAfter != null) 
        {
            filter &= builder.Gte(r => r.ModifiedAt, ModifiedAfter);
        }
        
        return filter;
    }
}
