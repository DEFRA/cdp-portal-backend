using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Secrets;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpoint
{
    public static void MapDeploymentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/deployments", FindLatestDeployments);
        app.MapGet("/deployments-with-migrations", FindLatestDeploymentsWithMigrations);
        app.MapGet("/deployments/{deploymentId}", FindDeployments);
        app.MapGet("/deployments/filters/", GetDeploymentsFilters);
        app.MapGet("/whats-running-where", WhatsRunningWhere);
        app.MapGet("/whats-running-where/{service}", WhatsRunningWhereForService);
        app.MapGet("/whats-running-where/filters", GetWhatsRunningWhereFilters);
        app.MapPost("/deployments", RegisterDeployment);
        app.MapGet("/deployment-settings/{service}/{environment}", FindDeploymentSettings);
    }

    // GET /deployments or with query params GET /deployments?environment=dev&service=forms-runner&user=jeff&status=running&page=1&offset=0&size=50
    private static async Task<IResult> FindLatestDeployments(IDeploymentsService deploymentsService,
        [FromQuery(Name = "favourites")] string[]? favourites,
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
            favourites,
            environment,
            service,
            user,
            status,
            team,
            offset ?? 0,
            page ?? DeploymentsService.DefaultPage,
            size ?? DeploymentsService.DefaultPageSize,
            cancellationToken
        );
        return Results.Ok(deploymentsPage);
    }
    
    private static async Task<IResult> FindLatestDeploymentsWithMigrations(IDeploymentsService deploymentsService,
        [FromQuery(Name = "favourites")] string[]? favourites,
        [FromQuery(Name = "environment")] string? environment,
        [FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "kind")] string? kind,
        [FromQuery(Name = "offset")] int? offset,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "size")] int? size,
        CancellationToken cancellationToken)
    {
        var deploymentsPage = await deploymentsService.FindLatestWithMigrations(
            favourites,
            environment,
            service,
            user,
            status,
            team,
            kind,
            offset ?? 0,
            page ?? DeploymentsService.DefaultPage,
            size ?? DeploymentsService.DefaultPageSize,
            cancellationToken
        );
        return Results.Ok(deploymentsPage);
    }

    // GET /deployments/filters
    private static async Task<IResult> GetDeploymentsFilters(
        IDeploymentsService deploymentsService,
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
    private static async Task<IResult> FindDeployments(IDeploymentsService deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);

        if (deployment == null) return Results.NotFound(new ApiError($"{deploymentId} was not found"));

        deployment.Secrets.Keys.Sort();
        return Results.Ok(deployment);
    }

    // GET /whats-running-where or with query params GET /whats-running-where?environments=dev&service=forms-runner&status=running
    private static async Task<IResult> WhatsRunningWhere(IDeploymentsService deploymentsService,
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

    // GET /whats-running-where/{service}
    private static async Task<IResult> WhatsRunningWhereForService(IDeploymentsService deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(service, cancellationToken);
        return Results.Ok(deployments);
    }

    // GET /whats-running-where/filters
    private static async Task<IResult> GetWhatsRunningWhereFilters(
        IDeploymentsService deploymentsService,
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
        IDeploymentsService deploymentsService,
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

        var deployment = Deployment.FromRequest(requestedDeployment);
        
        // Record what secrets the service has
        var secrets = await secretsService.FindServiceSecretsForEnvironment(deployment.Environment, deployment.Service, cancellationToken);
        if (secrets != null)
        {
            deployment.Secrets = secrets.AsTenantSecretKeys();
        }
        
        await deploymentsService.RegisterDeployment(deployment, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> FindDeploymentSettings(
        IDeploymentsService deploymentsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await deploymentsService.FindDeploymentSettings(service, environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }
}