using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Tenants;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantsEndpoint
{
    public static void MapTenantsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tenants", SearchTenants);
        app.MapGet("/tenants/{name}", FindTenant);
    }

    private static async Task<IResult> SearchTenants(
        ITenantService tenantsService,
        string? name,
        string? environment,
        string? team,
        CancellationToken cancellationToken)
    {
        var result = await tenantsService.FindAsync(new TenantFilter { Name = name, Environment = environment, TeamId = team}, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> FindTenant(
        ITenantService tenantsService,
        string name,
        CancellationToken cancellationToken)
    {
        var result = await tenantsService.FindOneAsync(name, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }

}