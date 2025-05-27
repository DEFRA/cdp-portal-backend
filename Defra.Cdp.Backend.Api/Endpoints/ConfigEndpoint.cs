using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ConfigEndpoint
{
    public static void MapConfigEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/config/latest/{environment}", LatestAppConfig);
        app.MapGet("/config/latest/{environment}/{repositoryName}", LatestAppConfigForRepository);
    }

    private static async Task<IResult> LatestAppConfig(
        IAppConfigVersionsService appConfigVersionsService,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await appConfigVersionsService.FindLatestAppConfigVersion(environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }

    private static async Task<IResult> LatestAppConfigForRepository(
        IAppConfigsService appConfigService,
        string environment,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        var result = await appConfigService.FindLatestAppConfig(environment, repositoryName, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }
}