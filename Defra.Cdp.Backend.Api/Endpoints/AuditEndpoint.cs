using Defra.Cdp.Backend.Api.Services.Audit;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AuditEndpoint
{
    private const string AuditBaseRoute = "audit";

    public static void MapAuditEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{AuditBaseRoute}", RecordAudit);
        app.MapGet($"{AuditBaseRoute}", FindAudits);
    }

    private static async Task<IResult> FindAudits(IAuditService auditService, CancellationToken cancellationToken)
    {
        var audits = await auditService.FindAll(cancellationToken);
        return Results.Ok(audits);
    }

    private static async Task<IResult> RecordAudit(IAuditService auditService, AuditDto auditDto,
        CancellationToken cancellationToken)
    {
        await auditService.Audit(auditDto, cancellationToken);
        return Results.Ok();
    }
}