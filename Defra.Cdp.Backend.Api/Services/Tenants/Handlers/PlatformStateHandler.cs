using System.IO.Compression;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;

namespace Defra.Cdp.Backend.Api.Services.Tenants.Handlers;

public class PlatformStateHandler(TenantService tenantService) : IMonoLambdaEventHandler
{
    public string EventType => "platform_state";
    
    public async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {

        if (!message.TryGetProperty("payload", out var payload))
        {
            throw new Exception("Platform state payload is missing a payload or was not a string");
        }

        // Check if payload is compressed
        message.TryGetProperty("compression", out var compression);
        var state = compression.GetString() switch
        {
            null => payload.Deserialize<PlatformStatePayload>(),
            "gzip" => DecompressAndDeserialize<PlatformStatePayload>(payload.GetString() ?? ""),
            _ => throw new Exception($"Unsupported compression {compression}")
        };

        if (state != null)
        {
            // TODO: check version/serials to decide if we can/should update
            await tenantService.UpdateState(state, cancellationToken);
        }
    }

    static T DecompressAndDeserialize<T>(string base64CompressedData) where T : new()
    {
        if (string.IsNullOrEmpty(base64CompressedData))
        {
            throw new ArgumentException("Base64 compressed data cannot be null or empty.", nameof(base64CompressedData));
        }

        var compressedBytes = Convert.FromBase64String(base64CompressedData);
        
        using var compressedStream = new MemoryStream(compressedBytes);
        using var decompressedStream = new GZipStream(compressedStream, CompressionMode.Decompress);

        var result = JsonSerializer.Deserialize<T>(decompressedStream);
        if (result == null)
        {
            throw new JsonException("Deserialization resulted in a null object, check the JSON structure.");
        }
        return result;
    }
}