using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class VanityUrlsEndpoint
{
    public static void MapVanityUrlsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vanity-urls/{service}/{environment}", ServiceVanityUrlsForEnv);
        app.MapGet("/vanity-urls/{service}", ServiceVanityUrls);
    }

    private static async Task<IResult> ServiceVanityUrlsForEnv(
        IVanityUrlsService vanityUrlsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var results = await vanityUrlsService.FindServiceByEnv(service, environment, cancellationToken);
        return results.Count == 0
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(new VanityUrlsResponse(results));
    }

    private static async Task<IResult> ServiceVanityUrls(
        IVanityUrlsService vanityUrlsService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await vanityUrlsService.FindService(service, cancellationToken);
        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result
            .GroupBy(g => g.Environment)
            .ToDictionary(k => k.Key, v => new VanityUrlsResponse(v.ToList()));
        
        return Results.Ok(response);
    }

    public class VanityUrlsResponse(List<VanityUrlRecord> vanityUrlsRecord)
    {
        [JsonPropertyName("vanityUrls")] public List<VanityUrlRecord> VanityUrls { get; } = vanityUrlsRecord;
    }
}