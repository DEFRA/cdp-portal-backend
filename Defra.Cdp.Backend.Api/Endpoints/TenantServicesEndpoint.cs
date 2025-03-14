using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

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
        var result = await tenantServicesService.FindOne( new TenantServiceFilter{name = service, environment = environment}, cancellationToken);
        return result == null
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(new TenantServicesResponse(result));
    }

    private static async Task<IResult> TenantService(
        ITenantServicesService tenantServicesService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await tenantServicesService.Find( new TenantServiceFilter{name = service}, cancellationToken);

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