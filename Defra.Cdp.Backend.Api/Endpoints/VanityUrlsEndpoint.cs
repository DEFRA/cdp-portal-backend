using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class VanityUrlsEndpoint
{
    public static void MapVanityUrlsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vanity-urls/{service}/{environment}", ServiceVanityUrl);
    }
    
    private static async Task<IResult> ServiceVanityUrl(
        IVanityUrlsService vanityUrlsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await vanityUrlsService.FindVanityUrls(service, environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(new VanityUrlsResponse(result));
    }

    private class VanityUrlsResponse
    {
        public VanityUrlsResponse(VanityUrlsRecord vanityUrlsRecord)
        {
            VanityUrls = vanityUrlsRecord.VanityUrls.Select(v => "https://" + v.Host + "." + v.Domain).ToList();
        }

        [JsonPropertyName("vanityUrls")] public List<string> VanityUrls { get; }
    }
}