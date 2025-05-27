using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Migrations;
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
    static async Task<IResult> ListServicesWithMigrations(IAvailableMigrations availableMigrations, CancellationToken cancellationToken)
    {
        var result = await availableMigrations.FindServicesWithMigrations(cancellationToken);
        return Results.Ok(result);
    }

    /**
     * List available schema versions for a given service.
     */
    static async Task<IResult> ListAvailableMigrationsForService(IAvailableMigrations availableMigrations, string service, CancellationToken cancellationToken)
    {
        var result = await availableMigrations.FindMigrationsForService(service, cancellationToken);
        return Results.Ok(result);
    }

    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<IResult> FindMigrationById(IDatabaseMigrationService migrationService, string id, CancellationToken cancellationToken)
    {
        var result = await migrationService.FindByCdpMigrationId(id, cancellationToken);
        return Results.Ok(result);
    }


    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<IResult> FindLatestForService(IDatabaseMigrationService migrationService, string service, CancellationToken cancellationToken)
    {
        var result = await migrationService.LatestForService(service, cancellationToken);
        return Results.Ok(result);
    }

    /**
     * Returns a specific run by the internal CDP Migration ID.
     */
    static async Task<IResult> SearchMigrationRuns(IDatabaseMigrationService migrationService,
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
        return Results.Ok(result);
    }


    /**
     * Triggers a new migration run.
     */
    static async Task<IResult> RunMigration(
        [FromServices] IDatabaseMigrationService migrationService,
        [FromServices] IRepositoryService repositoryService,
        [FromBody] DatabaseMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var migration = DatabaseMigration.FromRequest(request);
        await migrationService.CreateMigration(migration, cancellationToken);
        return Results.Ok();
    }
}