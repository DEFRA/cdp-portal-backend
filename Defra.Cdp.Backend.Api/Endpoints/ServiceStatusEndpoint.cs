using Defra.Cdp.Backend.Api.Services.TenantStatus;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ServiceStatusEndpoint
{
    public static void MapServiceStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/service-status/{service}", GetStatus);
    }

    private static async Task<IResult> GetStatus(ITenantStatusService tenantStatusService, string service, CancellationToken cancellationToken)
    {
        var status = await tenantStatusService.GetTenantStatus(service, cancellationToken);
        return Results.Ok(status);
    }
}