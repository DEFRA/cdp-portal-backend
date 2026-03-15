using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class MigrationEndpoints
{
    public static void MapMigrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/migrations/services", ListServicesWithMigrations);
        app.MapGet("/migrations/services/{service}", ListAvailableMigrationsForService);
        app.MapGet("/migrations/runs/{id}", FindMigrationById);
        app.MapGet("/migrations/runs", SearchMigrationRuns);
        app.MapGet("/migrations/latest/{service}", FindLatestForService);
        app.MapPost("/migrations/runs", RunMigration);
    }

    /**
     * List services that have database migrations available
    */
    static async Task<Ok<List<string>>> ListServicesWithMigrations(IAvailableMigrations availableMigrations, string[]? teamIds, CancellationToken cancellationToken)
    {
        if (teamIds is { Length: > 0 })
        {
            var migrationsByTeam =
                await availableMigrations.FindServicesWithMigrationsByTeam(teamIds.ToList(), cancellationToken);
            return TypedResults.Ok(migrationsByTeam);
        }

        var allMigrations = await availableMigrations.FindServicesWithMigrations(cancellationToken);
        return TypedResults.Ok(allMigrations);
    }

    /**
     * List available schema versions for a given service.
     */
    static async Task<Ok<List<MigrationVersion>>> ListAvailableMigrationsForService(IAvailableMigrations availableMigrations, string service, CancellationToken cancellationToken)
    {
        var result = await availableMigrations.FindMigrationsForService(service, cancellationToken);
        return TypedResults.Ok(result);
    }

    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<Ok<DatabaseMigration>> FindMigrationById(IDatabaseMigrationService migrationService, string id, CancellationToken cancellationToken)
    {
        var result = await migrationService.FindByCdpMigrationId(id, cancellationToken);
        return TypedResults.Ok(result);
    }


    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<Ok<List<DatabaseMigration>>> FindLatestForService(IDatabaseMigrationService migrationService, string service, CancellationToken cancellationToken)
    {
        var result = await migrationService.LatestForService(service, cancellationToken);
        return TypedResults.Ok(result);
    }

    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<Ok<List<DatabaseMigration>>> SearchMigrationRuns(IDatabaseMigrationService migrationService,
        string? cdpMigrationId,
        string? buildId,
        string? service,
        string? environment,
        string? status,
        CancellationToken cancellationToken)
    {
        var filter = new DatabaseMigrationFilter
        {
            Environment = environment,
            BuildId = buildId,
            CdpMigrationId = cdpMigrationId,
            Service = service,
            Status = status
        };
        var result = await migrationService.Find(filter, cancellationToken);
        return TypedResults.Ok(result);
    }


    /**
     * Triggers a new migration run.
     */
    static async Task<Ok> RunMigration(
        [FromServices] IDatabaseMigrationService migrationService,
        [FromServices] IRepositoryService repositoryService,
        [FromBody] DatabaseMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var migration = DatabaseMigration.FromRequest(request);
        await migrationService.CreateMigration(migration, cancellationToken);
        return TypedResults.Ok();
    }
}
