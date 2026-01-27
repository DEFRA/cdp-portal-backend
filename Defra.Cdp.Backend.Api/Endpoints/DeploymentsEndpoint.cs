using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Teams;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpoint
{
    public static void MapDeploymentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/deployments", FindLatestDeployments);
        app.MapGet("/deployments-with-migrations", FindLatestDeploymentsWithMigrations);
        app.MapGet("/deployments/{deploymentId}", FindDeployment);
        app.MapGet("/deployments/filters/", GetDeploymentsFilters);
        app.MapGet("/running-services", RunningServices);
        app.MapGet("/running-services/{service}", RunningServicesForService);
        app.MapGet("/running-services/filters", RunningServicesFilters);
        app.MapPost("/deployments", RegisterDeployment);
        app.MapGet("/deployment-settings/{service}/{environment}", FindDeploymentSettings);
    }

    // GET /deployments or with query params GET /deployments?environment=dev&service=forms-runner&user=jeff&status=running&page=1&offset=0&size=50
    private static async Task<IResult> FindLatestDeployments(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
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
        string[]? servicesForTeam = null;
        if (!string.IsNullOrWhiteSpace(team))
        {
            servicesForTeam = (await entitiesService.GetEntityIds(new EntityMatcher { TeamId = team }, cancellationToken)).ToArray();
        }

        var query = new DeploymentMatchers
        {
            Favourites = favourites,
            Environment = environment,
            Service = service,
            User = user,
            Status = status,
            Services = servicesForTeam,
        };

        var deploymentsPage = await deploymentsService.FindLatest(
            query,
            offset ?? 0,
            page ?? DeploymentsService.DefaultPage,
            size ?? DeploymentsService.DefaultPageSize,
            cancellationToken
        );
        return Results.Ok(deploymentsPage);
    }

    private static async Task<IResult> FindLatestDeploymentsWithMigrations(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
        [FromQuery(Name = "favourites")] string[]? favourites,
        [FromQuery(Name = "environment")] string? environment,
        [FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "teams")] string[]? teams,
        [FromQuery(Name = "kind")] string? kind,
        [FromQuery(Name = "offset")] int? offset,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "size")] int? size,
        CancellationToken cancellationToken)
    {
        string[]? servicesForTeam = null;
        if (!string.IsNullOrWhiteSpace(team))
        {
            servicesForTeam = (await entitiesService.GetEntityIds( new EntityMatcher {TeamId = team }, cancellationToken)).ToArray();
        }
        
        if (teams is { Length: > 0 })
        {
            servicesForTeam = (await entitiesService.GetEntityIds( new EntityMatcher {TeamIds = teams }, cancellationToken)).ToArray();
        }

        var query = new DeploymentMatchers
        {
            Favourites = favourites,
            Environment = environment,
            Service = service,
            User = user,
            Status = status,
            Services = servicesForTeam,
            Kind = kind
        };


        var deploymentsPage = await deploymentsService.FindLatestWithMigrations(
            query,
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
        ITeamsService teamsService,
        CancellationToken cancellationToken)
    {
        var deploymentFilters = await deploymentsService.GetDeploymentsFilters(cancellationToken);
        var teamRecord = await teamsService.FindAll(cancellationToken);
        deploymentFilters.Teams = teamRecord.Select(t => new RepositoryTeam(t.Github, t.TeamId, t.TeamName)).ToList();
        return Results.Ok(new { Filters = deploymentFilters });
    }

    // Get /deployments/{deploymentId}
    private static async Task<IResult> FindDeployment(IDeploymentsService deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);

        if (deployment == null) return Results.NotFound(new ApiError($"{deploymentId} was not found"));

        deployment.Secrets.Keys.Sort();
        return Results.Ok(deployment);
    }

    // GET /running-services or with query params GET /running-services?environments=dev&service=forms-runner&status=running
    private static async Task<IResult> RunningServices(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
        [FromQuery(Name = "environments")] string[]? environments,
        [FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "teams")] string[]? teams,
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "status")] string? status,
        CancellationToken cancellationToken)
    {
        string[]? servicesForTeam = null;
        if (!string.IsNullOrWhiteSpace(team))
        {
            servicesForTeam = (await entitiesService.GetEntityIds( new EntityMatcher {TeamId = team }, cancellationToken)).ToArray();
        }
        
        if (teams is { Length: > 0 })
        {
            servicesForTeam = (await entitiesService.GetEntityIds( new EntityMatcher {TeamIds = teams }, cancellationToken)).ToArray();
        }

        var deployments = await deploymentsService.RunningDeploymentsForService(
            new DeploymentMatchers
            {
                Environments = environments,
                Service = service,
                User = user,
                Services = servicesForTeam,
                Status = status
            },
            cancellationToken);
        return Results.Ok(deployments);
    }

    // GET /running-services/{service}
    private static async Task<IResult> RunningServicesForService(IDeploymentsService deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.RunningDeploymentsForService(service, cancellationToken);
        return Results.Ok(deployments);
    }

    // GET /running-services/filters
    private static async Task<IResult> RunningServicesFilters(
        IDeploymentsService deploymentsService,
        IUserServiceBackendClient userServiceBackendClient,
        CancellationToken cancellationToken)
    {
        var whatsRunningWhereFilters = await deploymentsService.GetWhatsRunningWhereFilters(cancellationToken);
        var teamRecord = await userServiceBackendClient.GetLatestCdpTeamsInformation(cancellationToken);
        if (teamRecord != null)
            whatsRunningWhereFilters.Teams =
                teamRecord.Select(t => new RepositoryTeam(t.github!, t.teamId, t.name)).ToList();
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