using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantSecretsEndpoint
{
    public static void MapTenantSecretsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("secrets/{service}/{environment}", FindTenantSecrets);
        app.MapGet("secrets/{service}", FindAllTenantSecrets);
        app.MapPost("secrets/{service}/{environment}/add", AddSecret);
        app.MapPost("secrets/{service}/{environment}/remove", RemoveSecret);
    }

    private static async Task<Results<NotFound<ApiError>, Ok<TenantSecretsResponse>>> FindTenantSecrets(
        [FromServices] ISecretsService secretsService,
        string service, string environment, CancellationToken cancellationToken)
    {
        var secrets = await secretsService.FindServiceSecretsForEnvironment(environment, service, cancellationToken);
        if (secrets == null) return TypedResults.NotFound(new ApiError("No secrets found"));

        secrets.Keys.Sort();

        return TypedResults.Ok(new TenantSecretsResponse(
            secrets.Service,
            secrets.Environment,
            secrets.Keys,
            secrets.LastChangedDate,
            secrets.CreatedDate));
    }

    private static async Task<Results<NotFound<ApiError>, Ok<Dictionary<string, TenantSecretKeys>>>> FindAllTenantSecrets(
        [FromServices] ISecretsService secretsService, string service, CancellationToken cancellationToken)
    {
        var allSecrets = await secretsService.FindAllServiceSecrets(service, cancellationToken);
        return allSecrets.Count != 0
            ? TypedResults.Ok(allSecrets)
            : TypedResults.NotFound(new ApiError("No secrets found"));
    }

    private static async Task<Results<BadRequest<ApiError>, ProblemHttpResult, Ok<ManageSecretsActionResponse>>> AddSecret(
        [FromServices] IManageSecretsClient manageSecretsClient,
        [FromServices] ISecretsService secretsService,
        string service,
        string environment,
        AddSecretMutationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await manageSecretsClient.AddSecretKeyValuePair(
            environment,
            BuildTenantSecretName(service),
            request.SecretKey,
            request.SecretValue,
            cancellationToken
        );

        if (!response.IsSuccess)
        {
            return response.StatusCode is >= System.Net.HttpStatusCode.BadRequest and < System.Net.HttpStatusCode.InternalServerError
                ? TypedResults.BadRequest(new ApiError(response.ErrorMessage ?? "Failed to add secret"))
                : TypedResults.Problem(detail: response.ErrorMessage, statusCode: StatusCodes.Status500InternalServerError);
        }

        await secretsService.AddSecretKey(environment, service, request.SecretKey, cancellationToken);
        return TypedResults.Ok(response.Response!);
    }

    private static async Task<Results<BadRequest<ApiError>, ProblemHttpResult, Ok<ManageSecretsActionResponse>>> RemoveSecret(
        [FromServices] IManageSecretsClient manageSecretsClient,
        [FromServices] ISecretsService secretsService,
        string service,
        string environment,
        RemoveSecretMutationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await manageSecretsClient.RemoveSecretKeyValuePair(
            environment,
            BuildTenantSecretName(service),
            request.SecretKey,
            cancellationToken
        );

        if (!response.IsSuccess)
        {
            return response.StatusCode is >= System.Net.HttpStatusCode.BadRequest and < System.Net.HttpStatusCode.InternalServerError
                ? TypedResults.BadRequest(new ApiError(response.ErrorMessage ?? "Failed to remove secret"))
                : TypedResults.Problem(detail: response.ErrorMessage, statusCode: StatusCodes.Status500InternalServerError);
        }

        await secretsService.RemoveSecretKey(environment, service, request.SecretKey, cancellationToken);
        return TypedResults.Ok(response.Response!);
    }

    private static string BuildTenantSecretName(string service) => $"cdp/services/{service}";

    private sealed record TenantSecretsResponse(
        string Service,
        string Environment,
        List<string> Keys,
        string LastChangedDate,
        string CreatedDate);

    private sealed record AddSecretMutationRequest(string SecretKey, string SecretValue);
    private sealed record RemoveSecretMutationRequest(string SecretKey);
}
