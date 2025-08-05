using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantServicesEndpoint
{
    public static void MapTenantServicesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tenant-services/{service}/{environment}", TenantServiceForEnv);
        app.MapGet("/tenant-services/{service}", TenantService);
    }

    private static async Task<IResult> TenantServiceForEnv(
        ITenantServicesService tenantServicesService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await tenantServicesService.FindOne(new TenantServiceFilter { Name = service, Environment = environment }, cancellationToken);
        return result == null
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(result);
    }

    private static async Task<IResult> TenantService(
        ITenantServicesService tenantServicesService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await tenantServicesService.Find(new TenantServiceFilter { Name = service }, cancellationToken);

        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result.ToDictionary(s => s.Environment,
            s => s);
        return Results.Ok(response);
    }

}