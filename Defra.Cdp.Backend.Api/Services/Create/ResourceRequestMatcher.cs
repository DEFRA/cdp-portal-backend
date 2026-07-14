using Defra.Cdp.Backend.Api.Services.Create.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public record ResourceRequestMatcher(
    string[]? Name,
    string[]? TeamIds,
    string[]? Status,
    string? UserId
)
{
    public FilterDefinition<ResourceRequestRecord> Match()
    {
        var builder = Builders<ResourceRequestRecord>.Filter;
        var filter = builder.Empty;

        
        if (Name != null)
        {
            filter &= builder.AnyIn(r => r.Entities, Name);
        }
        
        if (TeamIds != null)
        {
            filter &= builder.AnyIn(new StringFieldDefinition<ResourceRequestRecord>("teams.teamId"), TeamIds);
        }
        
        if (Status != null)
        {
            filter &= builder.In(r => r.Status, Status);
        }

        if (UserId != null)
        {
            filter &= builder.Eq(r => r.RequestedBy!.Id, UserId);
        }
        
        return filter;
    }
}
