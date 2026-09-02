using System.Net;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface IManageSecretsClient
{
    Task<ApiResult<ManageSecretsActionResponse>> AddSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        string secretKeyPairValue,
        CancellationToken cancellationToken
    );

    Task<ApiResult<ManageSecretsActionResponse>> RemoveSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        CancellationToken cancellationToken
    );
}

public class ManageSecretsClient(
    IOptions<MonoLambdaApiOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ManageSecretsClient> logger
) : MonoLambdaApiClient(options, httpClientFactory.CreateClient("ManageSecretsClient")), IManageSecretsClient
{
    public async Task<ApiResult<ManageSecretsActionResponse>> AddSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        string secretKeyPairValue,
        CancellationToken cancellationToken
    )
    {
        const string action = "add_secret_key_value_pair";
        var payload = new AddSecretRequest(
            SecretName: secretName,
            SecretKeyPairName: secretKeyPairName,
            SecretKeyPairValue: secretKeyPairValue
        );
        var (isSuccess, statusCode, responseBody) = await Send(
            environment, "/secrets/add-key-value-pair", payload, cancellationToken
        );

        return BuildResult(isSuccess, statusCode, responseBody, action, secretName, secretKeyPairName);
    }

    public async Task<ApiResult<ManageSecretsActionResponse>> RemoveSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        CancellationToken cancellationToken
    )
    {
        const string action = "remove_secret_key_value_pair";
        var payload = new RemoveSecretRequest(
            SecretName: secretName,
            SecretKeyPairName: secretKeyPairName
        );
        var (isSuccess, statusCode, responseBody) = await Send(
            environment, "/secrets/remove-key-value-pair", payload, cancellationToken
        );

        return BuildResult(isSuccess, statusCode, responseBody, action, secretName, secretKeyPairName);
    }

    private ApiResult<ManageSecretsActionResponse> BuildResult(
        bool isSuccess,
        HttpStatusCode statusCode,
        string responseBody,
        string action,
        string secretName,
        string secretKeyPairName
    )
    {
        if (!isSuccess)
        {
            return ApiResult<ManageSecretsActionResponse>.Failure(
                statusCode,
                ExtractErrorMessage(responseBody) ?? "Manage secrets request failed"
            );
        }

        logger.LogInformation(
            "Manage secrets API {Action} succeeded for {SecretName}/{SecretKeyPairName}",
            action,
            secretName,
            secretKeyPairName
        );
        return ApiResult<ManageSecretsActionResponse>.Success(
            new ManageSecretsActionResponse(action, secretName, secretKeyPairName)
        );
    }

    private async Task<(bool IsSuccess, HttpStatusCode StatusCode, string ResponseBody)> Send(
        string environment,
        string path,
        object payload,
        CancellationToken cancellationToken
    )
    {
        var (isSuccess, statusCode, responseBody) = await SendAsync(
            HttpMethod.Post,
            environment,
            path,
            payload,
            cancellationToken
        );
        if (!isSuccess)
        {
            logger.LogWarning(
                "Manage secrets API returned {StatusCode}: {Body}",
                (int)statusCode,
                responseBody
            );
        }

        return (isSuccess, statusCode, responseBody);
    }
}

public sealed record ManageSecretsActionResponse(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("secret_name")] string SecretName,
    [property: JsonPropertyName("secret_key_pair_name")] string SecretKeyPairName
);

public sealed record AddSecretRequest(
    [property: JsonPropertyName("secret_name")] string SecretName,
    [property: JsonPropertyName("secret_key_pair_name")] string SecretKeyPairName,
    [property: JsonPropertyName("secret_key_pair_value")] string SecretKeyPairValue
);

public sealed record RemoveSecretRequest(
    [property: JsonPropertyName("secret_name")] string SecretName,
    [property: JsonPropertyName("secret_key_pair_name")] string SecretKeyPairName
);
