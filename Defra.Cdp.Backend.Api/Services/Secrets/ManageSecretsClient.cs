using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Aws4RequestSigner;
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

    public Task<ManageSecretsResult> AddSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        string secretKeyPairValue,
        CancellationToken cancellationToken
    )
    {
        var payload = new AddSecretRequest(
            SecretName: secretName,
            SecretKeyPairName: secretKeyPairName,
            SecretKeyPairValue: secretKeyPairValue
        );
        return Send(environment, "/secrets/add-key-value-pair", payload, cancellationToken);
    }

    public Task<ManageSecretsResult> RemoveSecretKeyValuePair(
        string environment,
        string secretName,
        string secretKeyPairName,
        CancellationToken cancellationToken
    )
    {
        var payload = new RemoveSecretRequest(
            SecretName: secretName,
            SecretKeyPairName: secretKeyPairName
        );
        return Send(environment, "/secrets/remove-key-value-pair", payload, cancellationToken);
    }

    private async Task<ManageSecretsResult> Send(
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

        var credentials = FallbackCredentialsFactory.GetCredentials();
        var immutableCredentials = await credentials.GetCredentialsAsync();
        if (!string.IsNullOrEmpty(immutableCredentials.Token))
        {
            request.Headers.TryAddWithoutValidation("X-Amz-Security-Token", immutableCredentials.Token);
        }

        var region = System.Environment.GetEnvironmentVariable("AWS_REGION")
            ?? throw new InvalidOperationException("AWS_REGION environment variable is not set.");
        var signer = new AWS4RequestSigner(immutableCredentials.AccessKey, immutableCredentials.SecretKey);
        request = await signer.Sign(request, "execute-api", region);

        var response = await _client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var parsedBody = ParseActionResponse(responseBody);
            logger.LogInformation(
                "Manage secrets API {Action} succeeded for {SecretName}/{SecretKeyPairName}",
                parsedBody.Action,
                parsedBody.SecretName,
                parsedBody.SecretKeyPairName
            );
            return ManageSecretsResult.Success(parsedBody);
        }

        logger.LogWarning(
            "Manage secrets API returned {StatusCode}: {Body}",
            (int)response.StatusCode,
            responseBody
        );
        return ManageSecretsResult.Failure(
            response.StatusCode,
            ExtractErrorMessage(responseBody) ?? "Manage secrets request failed"
        );
    }

    private Uri BuildUri(string environment, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrlTemplate))
        {
            throw new InvalidOperationException("ManageSecretsApi BaseUrlTemplate is not configured.");
        }

        var baseUrl = _options.BaseUrlTemplate.Contains("{environment}", StringComparison.OrdinalIgnoreCase)
            ? _options.BaseUrlTemplate.Replace("{environment}", environment, StringComparison.OrdinalIgnoreCase)
            : _options.BaseUrlTemplate;

        return new Uri($"{baseUrl.TrimEnd('/')}{path}");
    }

    private static ManageSecretsActionResponse ParseActionResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (root.TryGetProperty("body", out var bodyElement))
        {
            root = bodyElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                var bodyString = root.GetString();
                if (!string.IsNullOrWhiteSpace(bodyString))
                {
                    return JsonSerializer.Deserialize<ManageSecretsActionResponse>(bodyString)
                           ?? throw new InvalidOperationException("Invalid manage-secrets response body.");
                }
            }
        }

        var response = root.Deserialize<ManageSecretsActionResponse>();
        return response ?? throw new InvalidOperationException("Invalid manage-secrets response.");
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
