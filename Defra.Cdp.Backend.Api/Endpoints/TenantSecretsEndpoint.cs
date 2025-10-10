using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantSecretsEndpoint
{
    public static void MapTenantSecretsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("secrets/{service}/{environment}", FindTenantSecrets);
        app.MapGet("secrets/{service}", FindAllTenantSecrets);
        app.MapPost("secrets/register/pending", RegisterPendingSecret);
    }

    private static async Task<IResult> FindTenantSecrets(
        [FromServices] ISecretsService secretsService,
        [FromServices] IPendingSecretsService pendingSecretsService,
        string service, string environment, CancellationToken cancellationToken)
    {
        var secrets = await secretsService.FindServiceSecretsForEnvironment(environment, service, cancellationToken);
        var pendingSecrets = await pendingSecretsService.FindPendingSecrets(environment, service, cancellationToken);

        if (secrets == null && pendingSecrets == null) return Results.NotFound(new ApiError("No secrets found"));

        var pendingSecretKeys = pendingSecrets?.Pending.Select(p => p.SecretKey).Distinct().ToList() ?? [];

        var exceptionMessage =
            await pendingSecretsService.PullExceptionMessage(environment, service, cancellationToken);

        pendingSecretKeys.Sort();
        secrets?.Keys.Sort();

        if (secrets == null)
        {
            return Results.Ok(new TenantSecretsResponse(
                pendingSecrets!.Service,
                pendingSecrets.Environment,
                pendingSecretKeys,
                pendingSecrets.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"),
                pendingSecrets.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"),
                pendingSecretKeys,
                exceptionMessage)
            );
        }
        
        return Results.Ok(new TenantSecretsResponse(
            secrets.Service,
            secrets.Environment,
            secrets.Keys,
            secrets.LastChangedDate,
            secrets.CreatedDate,
            pendingSecretKeys,
            exceptionMessage));
    }

    private static async Task<IResult> FindAllTenantSecrets(
        [FromServices] ISecretsService secretsService, string service, CancellationToken cancellationToken)
    {
        var allSecrets = await secretsService.FindAllServiceSecrets(service, cancellationToken);
        return allSecrets.Count != 0 ? Results.Ok(allSecrets) : Results.NotFound(new ApiError("No secrets found"));
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
        string Service,
        string Environment,
        List<string> Keys,
        string LastChangedDate,
        string CreatedDate,
        List<string>? Pending,
        string? ExceptionMessage);
}