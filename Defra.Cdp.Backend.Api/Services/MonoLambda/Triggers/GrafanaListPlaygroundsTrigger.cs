using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;

public record GrafanaListPlaygroundsTrigger : MongoLambdaTriggerPayload
{
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }
    
    [JsonPropertyName("service")]
    public required string Service { get; init; }
}