using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantServicesEndpoint
{
    public static void MapTenantServicesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tenant-services/{service}/{environment}", TenantService);
        app.MapGet("/tenant-services/{service}", AllTenantServices);
    }

    private static async Task<IResult> TenantService(
        ITenantServicesService tenantServicesService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await tenantServicesService.FindService(service, environment, cancellationToken);
        return result == null
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(new TenantServicesResponse(result));
    }

    private static async Task<IResult> AllTenantServices(
        ITenantServicesService tenantServicesService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await tenantServicesService.FindAllServices(service, cancellationToken);

        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result.ToDictionary(s => s.Environment,
            s => new TenantServicesResponse(s));
        return Results.Ok(response);
    }

    private class TenantServicesResponse(TenantServiceRecord record)
    {
        [JsonPropertyName("serviceCode")] public string ServiceCode { get; } = record.ServiceCode;
        [JsonPropertyName("zone")] public string Zone { get; } = record.Zone;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("testSuite")] public string? TestSuite { get; } = record.TestSuite;
    }
}