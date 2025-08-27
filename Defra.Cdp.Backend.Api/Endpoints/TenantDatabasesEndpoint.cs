using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantDatabasesEndpoint
{

    public static void MapTenantDatabasesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tenant-databases/{service}", FindAllForService);
        app.MapGet("/tenant-databases/{service}/{environment}", FindAllForServiceByEnv);
    }

    private static async Task<IResult> FindAllForService(
        [FromServices] ITenantRdsDatabasesService databasesService,
        string service,
        CancellationToken cancellationToken)
    {
        var results = await databasesService.Find(service, cancellationToken);
        return Results.Ok(results);
    }

    private static async Task<IResult> FindAllForServiceByEnv(
        [FromServices] ITenantRdsDatabasesService databasesService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var results = await databasesService.Find(service, environment, cancellationToken);
        return Results.Ok(results);
    }
}