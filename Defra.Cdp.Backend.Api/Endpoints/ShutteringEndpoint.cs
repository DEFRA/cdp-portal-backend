using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ShutteringEndpoint
{
    public static void MapShutteringEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shuttering/register", Register);
        app.MapGet("/shuttering/{serviceName}", ShutteringStatesForService);
        app.MapGet("/shuttering/{serviceName}/{url}", ShutteringStateForUrl);
    }

    private static async Task<Ok<List<ShutteringUrlState>>> ShutteringStatesForService(
        IShutteringService shutteringService, string serviceName,
        CancellationToken cancellationToken)
    {
        var shutteringRecords = await shutteringService.ShutteringStatesForService(serviceName, cancellationToken);
        return TypedResults.Ok(shutteringRecords);
    }

    private static async Task<Results<NotFound<ApiError>, Ok<ShutteringUrlState>>> ShutteringStateForUrl(
        IShutteringService shutteringService,
        string serviceName,
        string url,
        CancellationToken cancellationToken)
    {
        var shutteringState = await shutteringService.ShutteringStatesForService(serviceName, url, cancellationToken);
        return shutteringState == null
            ? TypedResults.NotFound(new ApiError("Shuttering record not found"))
            : TypedResults.Ok(shutteringState);
    }

    private static async Task<Ok> Register(IShutteringService shutteringService, ShutteringRecord shutteringRecord,
        CancellationToken cancellationToken)
    {
        await shutteringService.Register(shutteringRecord, cancellationToken);
        return TypedResults.Ok();
    }
}
