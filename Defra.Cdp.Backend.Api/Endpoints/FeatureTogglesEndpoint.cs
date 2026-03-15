using System.Net;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Microsoft.AspNetCore.Http.HttpResults;
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

    private static async Task<Ok<bool>> AnyToggleActiveForPath(IFeatureTogglesService featureTogglesService,
        string requestPath,
        CancellationToken cancellationToken)
    {
        var isActive =
            await featureTogglesService.IsAnyToggleActiveForPath(WebUtility.UrlDecode(requestPath), cancellationToken);
        return TypedResults.Ok(isActive);
    }

    private static async Task<Results<NotFound, Ok<FeatureToggle>, Ok<List<FeatureToggle>>>> GetFeatureToggles(IFeatureTogglesService featureTogglesService,
        [FromQuery(Name = "id")] string? toggleId,
        CancellationToken cancellationToken)
    {
        if (toggleId != null)
        {
            var toggle = await featureTogglesService.GetToggle(toggleId, cancellationToken);
            return toggle != null ? TypedResults.Ok(toggle) : TypedResults.NotFound();
        }
        var toggles = await featureTogglesService.GetAllToggles(cancellationToken);
        return TypedResults.Ok(toggles);
    }

    private static async Task<Ok> CreateFeatureToggle(
        IFeatureTogglesService featureTogglesService,
        FeatureToggle featureToggle,
        CancellationToken cancellationToken)
    {
        await featureTogglesService.CreateToggle(featureToggle, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<Ok> UpdateFeatureToggle(
        IFeatureTogglesService featureTogglesService,
        string featureToggleId, bool isActive,
        CancellationToken cancellationToken)
    {
        await featureTogglesService.UpdateToggle(featureToggleId, isActive, cancellationToken);
        return TypedResults.Ok();
    }
}
