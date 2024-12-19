using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class VanityUrlsEndpoint
{
    public static void MapVanityUrlsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vanity-urls/{service}/{environment}", ServiceVanityUrls);
        app.MapGet("/vanity-urls/{service}", AllServiceVanityUrls);
    }

    private static async Task<IResult> ServiceVanityUrls(
        IVanityUrlsService vanityUrlsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await vanityUrlsService.FindVanityUrls(service, environment, cancellationToken);
        return result == null
            ? Results.NotFound(new ApiError("Not found"))
            : Results.Ok(new VanityUrlsResponse(result));
    }

    private static async Task<IResult> AllServiceVanityUrls(
        IVanityUrlsService vanityUrlsService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await vanityUrlsService.FindAllVanityUrls(service, cancellationToken);

        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result.ToDictionary(vanityUrl => vanityUrl.Environment,
            vanityUrl => new VanityUrlsResponse(vanityUrl));
        return Results.Ok(response);
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