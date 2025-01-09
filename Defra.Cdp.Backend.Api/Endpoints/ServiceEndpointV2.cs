using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Service;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ServiceEndpointV2
{
    public static void MapServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v2/service/{name}", GetStatus);
    }

    private static async Task<IResult> GetStatus(IServiceOverviewService statusService, string name, CancellationToken cancellationToken)
    {
        var service = await statusService.GetService(name, cancellationToken);
        if (service == null || service.IsEmpty()) return Results.NotFound(new ApiError("Not Found"));
        return Results.Ok(service);
    }
}