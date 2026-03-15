using Defra.Cdp.Backend.Api.Services.Audit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AuditEndpoint
{
    public static void MapAuditEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/audit", RecordAudit);
        app.MapGet("/audit", FindAudits);
    }

    private static async Task<Ok<List<AuditDto>>> FindAudits(IAuditService auditService, CancellationToken cancellationToken)
    {
        var audits = await auditService.FindAll(cancellationToken);
        return TypedResults.Ok(audits);
    }

    private static async Task<Ok> RecordAudit(IAuditService auditService, AuditDto auditDto,
        CancellationToken cancellationToken)
    {
        await auditService.Audit(auditDto, cancellationToken);
        return TypedResults.Ok();
    }
}
