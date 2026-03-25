using System.ComponentModel.DataAnnotations;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.AspNetCore.Http.HttpResults;
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
    private static async Task<Ok<Paginated<Deployment>>> FindLatestDeployments(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "teamId")] string[]? teams,
        [AsParameters] DeploymentMatchers matchers,
        [AsParameters] Pagination pagination,
        CancellationToken cancellationToken)
    {
        var teamIds = new[] { team }
            .Concat(teams ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToArray();
        
        string[]? servicesForTeam = null;
        if (teamIds.Length > 0)
        {
            servicesForTeam = (await entitiesService.GetEntities( new EntityMatcher {TeamIds = teamIds }, cancellationToken)).Select(r => r.Name).ToArray();
        }

        var query = matchers with { Services = servicesForTeam };

        var deploymentsPage = await deploymentsService.FindLatest(
            query,
            pagination.Offset ?? 0,
            pagination.Page ?? DeploymentsService.DefaultPage,
            pagination.Size ?? DeploymentsService.DefaultPageSize,
            cancellationToken
        );
        return TypedResults.Ok(deploymentsPage);
    }

    private static async Task<Ok<Paginated<DeploymentOrMigration>>> FindLatestDeploymentsWithMigrations(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "teamId")] string[]? teams,        
        [AsParameters] DeploymentMatchers matchers,
        [AsParameters] Pagination pagination,
        CancellationToken cancellationToken)
    {
        
        var teamIds = new[] { team }
            .Concat(teams ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToArray();
        
        string[]? servicesForTeam = null;
        if (teamIds.Length > 0)
        {
            servicesForTeam = (await entitiesService.GetEntities( new EntityMatcher {TeamIds = teamIds }, cancellationToken)).Select(r => r.Name).ToArray();
        }

        var query = matchers with { Services = servicesForTeam };

        var deploymentsPage = await deploymentsService.FindLatestWithMigrations(
            query,
            pagination.Offset ?? 0,
            pagination.Page ?? DeploymentsService.DefaultPage,
            pagination.Size ?? DeploymentsService.DefaultPageSize,
            cancellationToken
        );
        return TypedResults.Ok(deploymentsPage);
    }

    // GET /deployments/filters
    private static async Task<Ok<DeploymentFiltersResponse>> GetDeploymentsFilters(
        IDeploymentsService deploymentsService,
        IUserServiceBackendClient userServiceBackendClient,
        CancellationToken cancellationToken)
    {
        var deploymentFilters = await deploymentsService.GetDeploymentsFilters(cancellationToken);
        var teamRecord = await userServiceBackendClient.GetLatestCdpTeamsInformation(cancellationToken);
        if (teamRecord != null)
        {
            deploymentFilters.Teams = teamRecord.Select(t => new RepositoryTeam(t.github!, t.teamId, t.name)).ToList();
        }
        return TypedResults.Ok(new DeploymentFiltersResponse{ Filters = deploymentFilters });
    }

    // Get /deployments/{deploymentId}
    private static async Task<Results<NotFound<ApiError>, Ok<Deployment>>> FindDeployment(IDeploymentsService deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);

        if (deployment == null) return TypedResults.NotFound(new ApiError($"{deploymentId} was not found"));

        deployment.Secrets.Keys.Sort();
        return TypedResults.Ok(deployment);
    }

    // GET /running-services or with query params GET /running-services?environment=dev&service=forms-runner&status=running
    private static async Task<Ok<List<Deployment>>> RunningServices(IDeploymentsService deploymentsService, IEntitiesService entitiesService,
        [FromQuery(Name = "team")] string? team,
        [FromQuery(Name = "teamId")] string[]? teams,        
        [AsParameters] DeploymentMatchers matchers,
        CancellationToken cancellationToken)
    {
        
        var teamIds = new[] { team }
            .Concat(teams ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToArray();
        
        string[]? servicesForTeam = null;
        if (teamIds.Length > 0)
        {
            servicesForTeam = (await entitiesService.GetEntities( new EntityMatcher {TeamIds = teamIds }, cancellationToken)).Select(r => r.Name).ToArray();
        }

        var deployments = await deploymentsService.RunningDeploymentsForService(
            matchers with { Services = servicesForTeam },
            cancellationToken);
        return TypedResults.Ok(deployments);
    }

    // GET /running-services/{service}
    private static async Task<Ok<List<Deployment>>> RunningServicesForService(IDeploymentsService deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.RunningDeploymentsForService(service, cancellationToken);
        return TypedResults.Ok(deployments);
    }

    // GET /running-services/filters
    private static async Task<Ok<DeploymentFiltersResponse>> RunningServicesFilters(
        IDeploymentsService deploymentsService,
        IUserServiceBackendClient userServiceBackendClient,
        CancellationToken cancellationToken)
    {
        var whatsRunningWhereFilters = await deploymentsService.GetWhatsRunningWhereFilters(cancellationToken);
        var teamRecord = await userServiceBackendClient.GetLatestCdpTeamsInformation(cancellationToken);
        if (teamRecord != null)
            whatsRunningWhereFilters.Teams =
                teamRecord.Select(t => new RepositoryTeam(t.github!, t.teamId, t.name)).ToList();
        return TypedResults.Ok(new DeploymentFiltersResponse { Filters = whatsRunningWhereFilters });
    }

    private static async Task<Results<BadRequest<IEnumerable<string?>>, Ok>> RegisterDeployment(
        IDeploymentsService deploymentsService,
        ISecretsService secretsService,
        RequestedDeployment requestedDeployment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(requestedDeployment, new ValidationContext(requestedDeployment), results, validateAllProperties: true);
        if (!isValid)
        {
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage));
        }

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
        return TypedResults.Ok();
    }

    private static async Task<Results<NotFound<ApiError>, Ok<DeploymentSettings>>> FindDeploymentSettings(
        IDeploymentsService deploymentsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await deploymentsService.FindDeploymentSettings(service, environment, cancellationToken);
        return result == null ? TypedResults.NotFound(new ApiError("Not found")) : TypedResults.Ok(result);
    }
}

public class DeploymentFiltersResponse
{
    public required DeploymentFilters Filters { get; init; }
}