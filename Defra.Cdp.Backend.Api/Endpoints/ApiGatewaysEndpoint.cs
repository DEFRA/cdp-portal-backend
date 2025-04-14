using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ApiGatewaysEndpoint
{
    public static void MapApiGatewaysEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api-gateways/{service}/{environment}", ServiceApiGatewaysForEnv);
        app.MapGet("/api-gateways/{service}", ServiceApiGateways);
    }

    private static async Task<IResult> ServiceApiGatewaysForEnv(
        IApiGatewaysService apiGatewaysService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var results = await apiGatewaysService.FindServiceByEnv(service, environment, cancellationToken);
        return results.Count == 0
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(new ApiGatewaysResponse(results));
    }

    private static async Task<IResult> ServiceApiGateways(
        IApiGatewaysService apiGatewaysService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await apiGatewaysService.FindService(service, cancellationToken);
        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result
            .GroupBy(g => g.Environment)
            .ToDictionary(k => k.Key, v => new ApiGatewaysResponse(v.ToList()));
        
        return Results.Ok(response);
    }

    public class ApiGatewaysResponse(List<ApiGatewayRecord> apiGatewaysRecord)
    {
        [JsonPropertyName("apiGateways")] public List<ApiGatewayRecord> ApiGateways { get; } = apiGatewaysRecord;
    }
}