using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record ResourceRequestPrClosedPayload
{
    [JsonPropertyName("prNumber")]
    public required int PrNumber { get; init; }
}