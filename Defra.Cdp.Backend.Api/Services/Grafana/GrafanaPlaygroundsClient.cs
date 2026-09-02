using System.Net;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPlaygroundsClient
{
    Task<ApiResult<GrafanaPlaygroundResources>> GetPlaygrounds(string service, CancellationToken cancellationToken);
}

public class GrafanaPlaygroundsClient(
    IOptions<MonoLambdaApiOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GrafanaPlaygroundsClient> logger
) : MonoLambdaApiClient(options, httpClientFactory.CreateClient("GrafanaPlaygroundsClient")), IGrafanaPlaygroundsClient
{
    public async Task<ApiResult<GrafanaPlaygroundResources>> GetPlaygrounds(string service, CancellationToken cancellationToken)
    {
        var path = $"/grafana/playgrounds/{Uri.EscapeDataString(service)}";
        var (isSuccess, statusCode, responseBody) = await SendAsync(
            HttpMethod.Get,
            CdpEnvironments.Dev,
            path,
            null,
            cancellationToken
        );
        if (!isSuccess)
        {
            logger.LogWarning(
                "Grafana playground API returned {StatusCode}: {Body}",
                (int)statusCode,
                responseBody
            );
            return ApiResult<GrafanaPlaygroundResources>.Failure(
                statusCode,
                ExtractErrorMessage(responseBody) ?? "Grafana playground request failed"
            );
        }

        var resources = ParseResponseBody(responseBody);
        if (resources == null)
        {
            logger.LogWarning("Grafana playground API returned malformed success body: {Body}", responseBody);
            return ApiResult<GrafanaPlaygroundResources>.Failure(HttpStatusCode.BadGateway, "Grafana playground response was invalid");
        }

        return ApiResult<GrafanaPlaygroundResources>.Success(resources);
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

            if (!root.TryGetProperty("body", out var bodyElement))
            {
                return null;
            }

            return JsonSerializer.Deserialize<GrafanaPlaygroundResources>(bodyElement.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
