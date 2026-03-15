using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ConfigEndpoint
{
    public static void MapConfigEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/config/latest/{environment}", LatestAppConfig);
        app.MapGet("/config/latest/{environment}/{repositoryName}", LatestAppConfigForRepository);
    }

    private static async Task<Results<NotFound<ApiError>, Ok<AppConfigVersion>>> LatestAppConfig(
        IAppConfigVersionsService appConfigVersionsService,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await appConfigVersionsService.FindLatestAppConfigVersion(environment, cancellationToken);
        return result == null ? TypedResults.NotFound(new ApiError("Not found")) : TypedResults.Ok(result);
    }

    private static async Task<Results<NotFound<ApiError>, Ok<AppConfig> >> LatestAppConfigForRepository(
        IAppConfigsService appConfigService,
        string environment,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        var result = await appConfigService.FindLatestAppConfig(environment, repositoryName, cancellationToken);
        return result == null ? TypedResults.NotFound(new ApiError("Not found")) : TypedResults.Ok(result);
    }
}