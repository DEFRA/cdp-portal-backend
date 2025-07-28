using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Terminal;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TerminalEndpoint
{

    public static void MapTerminalEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("terminal", RecordTerminalSession);
    }

    static async Task<IResult> RecordTerminalSession([FromServices] ITerminalService terminalService, TerminalSession session,
        CancellationToken cancellationToken)
    {
        await terminalService.CreateTerminalSession(session, cancellationToken);
        return Results.Created();
    }
}