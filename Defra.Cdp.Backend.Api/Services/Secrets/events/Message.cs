using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Secrets.events;

/**
 * Secret Manager Lambda sends various messages. We can parse the first few fields to work
 * out how to parse the body.
 *  {"source": "cdp-secret-manager-lambda",
 *   "statusCode": 200,
 *   "action": "get_all_secret_keys",
 *   "body": {  depends on 'action' type  }
 *  }'
 */
public record MessageHeader
{
    [JsonPropertyName("statusCode")] public int? StatusCode { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("action")] public string? Action { get; init; }
    [JsonPropertyName("body")] public JsonObject? Body { get; init; }
}

public record BodyGetAllSecretKeys
{
    [JsonPropertyName("secretKeys")] public Dictionary<string, SecretKey> SecretKeys { get; init; } = new();
    [JsonPropertyName("exception")] public string Exception { get; init; } = "";
    [JsonPropertyName("environment")] public string Environment { get; init; } = "";
}
