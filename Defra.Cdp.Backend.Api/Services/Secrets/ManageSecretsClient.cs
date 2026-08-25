using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime.Credentials;
using Defra.Cdp.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface IManageSecretsClient
{
    Task<ManageSecretsResult> AddSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        string secretKeyPairValue,
        CancellationToken cancellationToken
    );

    Task<ManageSecretsResult> RemoveSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        CancellationToken cancellationToken
    );
}

public class ManageSecretsClient(
    IOptions<ManageSecretsApiOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ManageSecretsClient> logger
) : IManageSecretsClient
{
    private readonly ManageSecretsApiOptions _options = options.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient("ManageSecretsClient");

    public async Task<ManageSecretsResult> AddSecretKeyValuePair(
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

    public async Task<ManageSecretsResult> RemoveSecretKeyValuePair(
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

    private ManageSecretsResult BuildResult(
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
            return ManageSecretsResult.Failure(
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
        return ManageSecretsResult.Success(
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
        var uri = BuildUri(environment, path);
        var requestBody = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var region = System.Environment.GetEnvironmentVariable("AWS_REGION")!;
        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();

        var response = await _client.SendAsync(request, region, "execute-api", credentials, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Manage secrets API returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                responseBody
            );
        }

        return (response.IsSuccessStatusCode, response.StatusCode, responseBody);
    }

    private Uri BuildUri(string environment, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrlTemplate))
        {
            throw new InvalidOperationException("ManageSecretsApi BaseUrlTemplate is not configured.");
        }

        var baseUrl = _options.BaseUrlTemplate.Replace("{environment}", environment, StringComparison.OrdinalIgnoreCase);

        return new Uri($"{baseUrl.TrimEnd('/')}{path}");
    }

    private static string? ExtractErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }

            if (root.TryGetProperty("body", out var bodyElement))
            {
                return bodyElement.ValueKind switch
                {
                    JsonValueKind.String => bodyElement.GetString(),
                    JsonValueKind.Object when bodyElement.TryGetProperty("message", out var nestedMessageElement) => nestedMessageElement.GetString(),
                    _ => bodyElement.ToString()
                };
            }
        }
        catch (JsonException)
        {
            return responseBody;
        }

        return responseBody;
    }
}

public sealed record ManageSecretsResult(bool IsSuccess, HttpStatusCode StatusCode, string? ErrorMessage, ManageSecretsActionResponse? Response)
{
    public static ManageSecretsResult Success(ManageSecretsActionResponse response) =>
        new(true, HttpStatusCode.OK, null, response);

    public static ManageSecretsResult Failure(HttpStatusCode statusCode, string message) =>
        new(false, statusCode, message, null);
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
