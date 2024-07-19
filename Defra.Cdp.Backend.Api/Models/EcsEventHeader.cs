using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

// Used to extract message type from ECS message so we can re-parse it with the correct parser.
public sealed record EcsEventHeader(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("detail-type")] string DetailType
);