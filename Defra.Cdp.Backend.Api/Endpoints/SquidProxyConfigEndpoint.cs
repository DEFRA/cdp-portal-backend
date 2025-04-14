using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class SquidProxyConfigEndpoint
{
    public static void MapSquidProxyConfigEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/squid-proxy-config/{service}/{environment}", ServiceSquidProxyConfig);
    }
    
    private static async Task<IResult> ServiceSquidProxyConfig(
        ISquidProxyConfigService squidProxyConfigService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await squidProxyConfigService.FindSquidProxyConfig(service, environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(new SquidProxyConfigResponse(result));
    }

    private class SquidProxyConfigResponse
    {
        public SquidProxyConfigResponse(SquidProxyConfigRecord squidProxyConfigRecord)
        {
            DefaultDomains = squidProxyConfigRecord.DefaultDomains;
            AllowedDomains = squidProxyConfigRecord.AllowedDomains;
        }

        [JsonPropertyName("defaultDomains")] public List<string> DefaultDomains { get; }
        [JsonPropertyName("allowedDomains")] public List<string> AllowedDomains { get; }
    }
}