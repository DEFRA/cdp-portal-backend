using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantSecretsEndpoint
{
    public static IEndpointRouteBuilder MapTenantSecretsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("secrets/{environment}/{service}", FindTenantSecrets);
        app.MapGet("secrets/{service}", FindAllTenantSecrets);
        app.MapPost("secrets/register/pending", RegisterPendingSecret);
        return app;
    }

    private static async Task<IResult> FindTenantSecrets(
        [FromServices] ISecretsService secretsService,
        [FromServices] IPendingSecretsService pendingSecretsService,
        string environment, string service, CancellationToken cancellationToken)
    {
        var secrets = await secretsService.FindSecrets(environment, service, cancellationToken);
        if (secrets == null) return Results.NotFound(new ApiError("No secrets found"));

        var pendingSecrets = await pendingSecretsService.FindPendingSecrets(environment, service, cancellationToken);
        var pendingSecretKeys = pendingSecrets?.Pending.Select(p => p.SecretKey).Distinct().ToList() ?? new List<string>();

        var exceptionMessage =
            await pendingSecretsService.PullExceptionMessage(environment, service, cancellationToken);

        pendingSecretKeys?.Sort();
        secrets.Keys.Sort();

        return Results.Ok( new TenantSecretsResponse(
            secrets.Service,
            secrets.Environment,
            secrets.Keys,
            secrets.LastChangedDate,
            secrets.CreatedDate,
            pendingSecretKeys,
            exceptionMessage)
        );
    }

    private static async Task<IResult> FindAllTenantSecrets(
        [FromServices] ISecretsService secretsService, string service, CancellationToken cancellationToken)
    {
        var allSecrets = await secretsService.FindAllSecrets(service, cancellationToken);
        return !allSecrets.Any() ?  Results.NotFound(new ApiError("No secrets found")) : Results.Ok(allSecrets);
    }

    private static async Task<IResult> RegisterPendingSecret(
        [FromServices] IPendingSecretsService pendingSecretsService,
        RegisterPendingSecret registerPendingSecret,
        CancellationToken cancellationToken)
    {
        await pendingSecretsService.RegisterPendingSecret(registerPendingSecret, cancellationToken);
        return Results.Ok(registerPendingSecret);
    }

    private sealed record TenantSecretsResponse(
        string Message,
        string Service,
        string Environment,
        List<string> Keys,
        string LastChangedDate,
        string CreatedDate,
        List<string>? Pending,
        string? ExceptionMessage)
    {
        public TenantSecretsResponse(
            string service, string environment, List<string> keys, string lastChangedDate, string createdDate,
            List<string>? pending, string? exceptionMessage) : this(
            "success",
            service,
            environment,
            keys,
            lastChangedDate,
            createdDate,
            pending,
            exceptionMessage)
        {
        }
    }
}