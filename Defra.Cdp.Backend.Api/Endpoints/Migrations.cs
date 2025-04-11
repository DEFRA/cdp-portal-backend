using Defra.Cdp.Backend.Api.Services.Migrations;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class Migrations
{
    public static void MapMigrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/migrations/available/{service}",
            async (IAvailableMigrations availableMigrations, string service, CancellationToken cancellationToken) =>
                await availableMigrations.FindMigrationsForService(service, cancellationToken));
    }
}