using System.Net;
using System.Text.Json;
using Amazon.Runtime.Credentials;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPlaygroundsClient
{
    Task<GrafanaPlaygroundsResult> GetPlaygrounds(string service, CancellationToken cancellationToken);
}

public class GrafanaPlaygroundsClient(
    IOptions<MonoLambdaApiOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GrafanaPlaygroundsClient> logger
) : IGrafanaPlaygroundsClient
{
    private readonly MonoLambdaApiOptions _options = options.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient("GrafanaPlaygroundsClient");

    public async Task<GrafanaPlaygroundsResult> GetPlaygrounds(string service, CancellationToken cancellationToken)
    {
        var path = $"/grafana/playgrounds/{Uri.EscapeDataString(service)}";
        var uri = BuildUri(CdpEnvironments.Dev, path);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

        var region = System.Environment.GetEnvironmentVariable("AWS_REGION")!;
        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();

        var response = await _client.SendAsync(request, region, "execute-api", credentials, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Grafana playground API returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                responseBody
            );
            return GrafanaPlaygroundsResult.Failure(
                response.StatusCode,
                ExtractErrorMessage(responseBody) ?? "Grafana playground request failed"
            );
        }

        var resources = ParseResponseBody(responseBody);
        if (resources == null)
        {
            logger.LogWarning("Grafana playground API returned malformed success body: {Body}", responseBody);
            return GrafanaPlaygroundsResult.Failure(HttpStatusCode.BadGateway, "Grafana playground response was invalid");
        }

        return GrafanaPlaygroundsResult.Success(resources);
    }

    private Uri BuildUri(string environment, string path)
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

    private static GrafanaPlaygroundResources? ParseResponseBody(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.TryGetProperty("body", out var bodyElement))
            {
                return bodyElement.ValueKind switch
                {
                    JsonValueKind.Object => JsonSerializer.Deserialize<GrafanaPlaygroundResources>(bodyElement.GetRawText()),
                    JsonValueKind.String => JsonSerializer.Deserialize<GrafanaPlaygroundResources>(bodyElement.GetString() ?? string.Empty),
                    _ => null
                };
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return JsonSerializer.Deserialize<GrafanaPlaygroundResources>(root.GetString() ?? string.Empty);
            }

            return JsonSerializer.Deserialize<GrafanaPlaygroundResources>(root.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
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

public sealed record GrafanaPlaygroundsResult(
    bool IsSuccess,
    HttpStatusCode StatusCode,
    string? ErrorMessage,
    GrafanaPlaygroundResources? Response
)
{
    public static GrafanaPlaygroundsResult Success(GrafanaPlaygroundResources response) =>
        new(true, HttpStatusCode.OK, null, response);

    public static GrafanaPlaygroundsResult Failure(HttpStatusCode statusCode, string message) =>
        new(false, statusCode, message, null);
}
