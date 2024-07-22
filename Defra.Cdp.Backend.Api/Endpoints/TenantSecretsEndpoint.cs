using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantSecretsEndpoint
{
    public static IEndpointRouteBuilder MapTenantSecretsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("secrets/{environment}/{service}", FindTenantSecrets);
        return app;
    }

    static async Task<IResult> FindTenantSecrets([FromServices] ISecretsService secretsService, string environment,
        string service, CancellationToken cancellationToken)
    {
        var secrets = await secretsService.FindSecrets(environment, service, cancellationToken);
        if (secrets == null) return Results.NotFound(new ApiError("secrets not found"));

        secrets.Keys.Sort();
        return Results.Ok(secrets);
    }
}