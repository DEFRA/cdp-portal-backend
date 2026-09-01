using System.Net;
using System.Text;
using System.Text.Json;
using SystemEnvironment = System.Environment;
using Amazon.Runtime.Credentials;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda;

public abstract class MonoLambdaApiClient(IOptions<MonoLambdaApiOptions> options, HttpClient client)
{
    private readonly MonoLambdaApiOptions _options = options.Value;

    protected async Task<(bool IsSuccess, HttpStatusCode StatusCode, string ResponseBody)> SendAsync(
        HttpMethod method,
        string environment,
        string path,
        object? payload,
        CancellationToken cancellationToken
    )
    {
        var uri = BuildUri(environment, path);
        var request = new HttpRequestMessage(method, uri);
        if (payload != null)
        {
            var requestBody = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        var region = SystemEnvironment.GetEnvironmentVariable("AWS_REGION")!;
        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();

        var response = await client.SendAsync(request, region, "execute-api", credentials, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, response.StatusCode, responseBody);
    }

    protected Uri BuildUri(string environment, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrlTemplate))
        {
            throw new InvalidOperationException("MonoLambdaApi BaseUrlTemplate is not configured.");
        }

        var restApiId = _options.RestApiIds
            .FirstOrDefault(mapping => string.Equals(mapping.Environment, environment, StringComparison.OrdinalIgnoreCase))
            ?.RestApiId;

        if (string.IsNullOrWhiteSpace(restApiId))
        {
            throw new InvalidOperationException(
                $"No mono-lambda API Gateway REST API id configured for environment '{environment}'."
            );
        }

        var baseUrl = _options.BaseUrlTemplate
            .Replace("{restApiId}", restApiId, StringComparison.OrdinalIgnoreCase)
            .Replace("{environment}", environment, StringComparison.OrdinalIgnoreCase);

        return new Uri($"{baseUrl.TrimEnd('/')}{path}");
    }

    protected static string? ExtractErrorMessage(string responseBody)
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
