using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class RequestedAnnotation
{
    [property: JsonPropertyName("title")] public string Title { get; init; } = default!;

    [property: JsonPropertyName("description")]
    public string Description { get; init; } = default!;
}