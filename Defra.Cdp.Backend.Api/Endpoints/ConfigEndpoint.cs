using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Actions;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ConfigEndpoint
{
    public static void MapConfigEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/config/latest/{environment}", LatestAppConfig);
    }
    private static async Task<IResult> LatestAppConfig(
        IAppConfigVersionService appConfigVersionService,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await appConfigVersionService.FindLatestAppConfigVersion(environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }
}