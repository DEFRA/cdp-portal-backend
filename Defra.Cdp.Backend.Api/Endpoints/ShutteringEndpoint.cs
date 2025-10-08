using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Shuttering;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ShutteringEndpoint
{
    public static void MapShutteringEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shuttering/register", Register);
        app.MapGet("/shuttering/{serviceName}", ShutteringStatesForService);
        app.MapGet("/shuttering/url/{url}", ShutteringStateForUrl);
    }

    [Obsolete("Use TenantService instead")]
    private static async Task<IResult> ShutteringStatesForService(
        IShutteringService shutteringService, string serviceName,
        CancellationToken cancellationToken)
    {
        var shutteringRecords = await shutteringService.ShutteringStatesForService(serviceName, cancellationToken);
        return Results.Ok(shutteringRecords);
    }

    [Obsolete("Use TenantService instead")]
    private static async Task<IResult> ShutteringStateForUrl(
        IShutteringService shutteringService, string url,
        CancellationToken cancellationToken)
    {
        var shutteringState = await shutteringService.ShutteringStateForUrl(url, cancellationToken);
        return shutteringState == null ? Results.NotFound(new ApiError("Shuttering record not found")) : Results.Ok(shutteringState);
    }

    private static async Task<IResult> Register(IShutteringService shutteringService, ShutteringRecord shutteringRecord,
        CancellationToken cancellationToken)
    {
        await shutteringService.Register(shutteringRecord, cancellationToken);
        return Results.Ok();
    }
}