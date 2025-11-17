using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Audit;
using Defra.Cdp.Backend.Api.Services.Terminal;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TerminalEndpoint
{

    public static void MapTerminalEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/terminals", RecordTerminalSession);
    }

    private static async Task<IResult> RecordTerminalSession(
        [FromServices] ITerminalService terminalService,
        [FromServices] IAuditService auditService,
        TerminalSession session,
        CancellationToken cancellationToken)
    {
        await terminalService.CreateTerminalSession(session, cancellationToken);
        if (session.Environment == "prod")
        {
            await auditService.Audit(CreateAuditDto(session), cancellationToken);
        }
        return Results.Created();
    }

    private static AuditDto CreateAuditDto(TerminalSession session)
    {
        return new AuditDto
        (
            "breakGlass",
            "TerminalAccess",
            session.User,
            DateTime.UtcNow,
            JsonDocument.Parse($$"""
                                 {
                                     "environment": "{{session.Environment}}",
                                     "service": "{{session.Service}}"
                                 }
                                 """).RootElement
            );
    }
}