using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Shuttering;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ShutteringEndpoint
{
    public static void MapShutteringEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shuttering/register", Register);
        app.MapGet("/shuttering/{serviceName}", ShutteringRecordsForService);
    }

    private static async Task<IResult> ShutteringRecordsForService(
        IShutteringService shutteringService, string serviceName,
        CancellationToken cancellationToken)
    {
        var shutteringRecords = await shutteringService.ShutteringRecordsForService(serviceName, cancellationToken);
        return Results.Ok(shutteringRecords);
    }

    private static async Task<IResult> Register(IShutteringService shutteringService, ShutteringRecord shutteringRecord,
        CancellationToken cancellationToken)
    {
        await shutteringService.Register(shutteringRecord, cancellationToken);
        return Results.Ok();
    }
}