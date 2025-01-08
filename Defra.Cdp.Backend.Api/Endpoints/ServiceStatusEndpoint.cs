using Defra.Cdp.Backend.Api.Services.Status;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ServiceStatusEndpoint
{
    public static void MapServiceStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/service-status/{service}", GetStatus);
    }

    private static async Task<IResult> GetStatus(IStatusService statusService, string service, CancellationToken cancellationToken)
    {
        var status = await statusService.GetTenantStatus(service, cancellationToken);
        return Results.Ok(status);
    }
}