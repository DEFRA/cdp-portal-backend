using System.Net;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class FeatureTogglesEndpoint
{
    public static void MapFeatureTogglesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/feature-toggles", CreateFeatureToggle);
        app.MapPut("/feature-toggles/{featureToggleId}/{isActive:bool}", UpdateFeatureToggle);
        app.MapGet("/feature-toggles", GetFeatureToggles);
        app.MapGet("/feature-toggles/{requestPath}", AnyToggleActiveForPath);
    }

    private static async Task<IResult> AnyToggleActiveForPath(IFeatureTogglesService featureTogglesService,
        string requestPath,
        CancellationToken cancellationToken)
    {
        var isActive =
            await featureTogglesService.IsAnyToggleActiveForPath(WebUtility.UrlDecode(requestPath), cancellationToken);
        return Results.Ok(isActive);
    }

    private static async Task<IResult> GetFeatureToggles(IFeatureTogglesService featureTogglesService,
        [FromQuery(Name = "id")] string? toggleId,
        CancellationToken cancellationToken)
    {
        if (toggleId != null)
        {
            var toggle = await featureTogglesService.GetToggle(toggleId, cancellationToken);
            return toggle != null ? Results.Ok(toggle) : Results.NotFound();
        }
        var toggles = await featureTogglesService.GetAllToggles(cancellationToken);
        return Results.Ok(toggles);
    }

    private static async Task<IResult> CreateFeatureToggle(
        IFeatureTogglesService featureTogglesService,
        FeatureToggle featureToggle,
        CancellationToken cancellationToken)
    {
        await featureTogglesService.CreateToggle(featureToggle, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateFeatureToggle(
        IFeatureTogglesService featureTogglesService,
        string featureToggleId, bool isActive,
        CancellationToken cancellationToken)
    {
        await featureTogglesService.UpdateToggle(featureToggleId, isActive, cancellationToken);
        return Results.Ok();
    }
}