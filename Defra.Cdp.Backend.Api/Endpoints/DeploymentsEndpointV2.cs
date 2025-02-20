using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Secrets;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpointV2
{
    public static void MapDeploymentsEndpointV2(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v2/deployments", FindLatestDeployments);
        app.MapGet("/v2/deployments/{deploymentId}", FindDeployments);
        app.MapGet("/v2/deployments/filters/", GetDeploymentsFilters);
        app.MapGet("/v2/whats-running-where", WhatsRunningWhere);
        app.MapGet("/v2/whats-running-where/{service}", WhatsRunningWhereForService);
        app.MapGet("/v2/whats-running-where/filters", GetWhatsRunningWhereFilters);
        app.MapPost("/v2/deployments", RegisterDeployment);
        app.MapPost("/deployments", RegisterDeployment); // fallback while we migrate self-service-ops off v1
        app.MapGet("/v2/deployment-config/{service}/{environment}", DeploymentConfig);
    }

    // GET /v2/deployments or with query params GET /v2/deployments?environment=dev&service=forms-runner&user=jeff&status=running&page=1&offset=0&size=50
    private static async Task<IResult> FindLatestDeployments(IDeploymentsServiceV2 deploymentsService,
        [FromQuery(Name = "favouriteTeamIds")] string[]? favouriteTeamIds,
        [FromQuery(Name = "environment")] string? environment,
        [FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "offset")] int? offset,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "size")] int? size,
        CancellationToken cancellationToken)
    {
        var deploymentsPage = await deploymentsService.FindLatest(
            favouriteTeamIds,
            environment,
            service,
            user,
            status,
            team,
            offset ?? 0,
            page ?? DeploymentsServiceV2.DefaultPage,
            size ?? DeploymentsServiceV2.DefaultPageSize,
            cancellationToken
        );
        return Results.Ok(deploymentsPage);
    }

    // GET /v2/deployments/filters
    private static async Task<IResult> GetDeploymentsFilters(
        IDeploymentsServiceV2 deploymentsService,
        IUserServiceFetcher userServiceFetcher,
        CancellationToken cancellationToken)
    {
        var deploymentFilters = await deploymentsService.GetDeploymentsFilters(cancellationToken);
        var teamRecord = await userServiceFetcher.GetLatestCdpTeamsInformation(cancellationToken);
        if (teamRecord != null)
        {
            deploymentFilters.Teams = teamRecord.teams.Select(t => new RepositoryTeam(t.github,  t.teamId, t.name)).ToList();
        }
        return Results.Ok(new { Filters = deploymentFilters });
    }

    // Get /deployments/{deploymentId}
    private static async Task<IResult> FindDeployments(IDeploymentsServiceV2 deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);

        if (deployment == null) return Results.NotFound(new ApiError($"{deploymentId} was not found"));

        deployment.Secrets.Keys.Sort();
        return Results.Ok(deployment);
    }

    // GET /v2/whats-running-where or with query params GET /v2/whats-running-where?environments=dev&service=forms-runner&status=running
    private static async Task<IResult> WhatsRunningWhere(IDeploymentsServiceV2 deploymentsService,
        [FromQuery(Name = "environments")] string[]? environments,
        [FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "status")] string? status,
        CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(environments, service,
            team, user, status, cancellationToken);
        return Results.Ok(deployments);
    }

    // GET /v2/whats-running-where/{service}
    private static async Task<IResult> WhatsRunningWhereForService(IDeploymentsServiceV2 deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(service, cancellationToken);
        return Results.Ok(deployments);
    }

    // GET /v2/whats-running-where/filters
    private static async Task<IResult> GetWhatsRunningWhereFilters(
        IDeploymentsServiceV2 deploymentsService,
        IUserServiceFetcher userServiceFetcher,
        CancellationToken cancellationToken)
    {
        var whatsRunningWhereFilters = await deploymentsService.GetWhatsRunningWhereFilters(cancellationToken);
        var teamRecord = await userServiceFetcher.GetLatestCdpTeamsInformation(cancellationToken);
        if (teamRecord != null)
            whatsRunningWhereFilters.Teams =
                teamRecord.teams.Select(t => new RepositoryTeam(t.github,  t.teamId, t.name)).ToList();
        return Results.Ok(new { Filters = whatsRunningWhereFilters });
    }

    private static async Task<IResult> RegisterDeployment(
        IDeploymentsServiceV2 deploymentsServiceV2,
        ISecretsService secretsService,
        IValidator<RequestedDeployment> validator,
        RequestedDeployment requestedDeployment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(requestedDeployment, cancellationToken);
        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());
        
        var logger = loggerFactory.CreateLogger("RegisterDeployment");
        logger.LogInformation("Registering deployment {DeploymentId}", requestedDeployment.DeploymentId);

        var deployment = DeploymentV2.FromRequest(requestedDeployment);
        
        // Record what secrets the service has
        var secrets = await secretsService.FindSecrets(deployment.Environment, deployment.Service, cancellationToken);
        if (secrets != null)
        {
            deployment.Secrets = secrets.AsTenantSecretKeys();
        }
        
        await deploymentsServiceV2.RegisterDeployment(deployment, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> DeploymentConfig(
        IDeploymentsServiceV2 deploymentsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await deploymentsService.FindDeploymentSettings(service, environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }
}