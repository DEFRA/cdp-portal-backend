using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;
using Defra.Cdp.Backend.Api.Services.Tenants.Models;
using ThirdParty.Json.LitJson;
using JsonException = System.Text.Json.JsonException;

namespace Defra.Cdp.Backend.Api.Services.Tenants.Handlers;

class Header
{
    [property: JsonPropertyName("payload_version")]
    public string? PayloadVersion { get; init; }
    [property: JsonPropertyName("compression")]
    public string? Compression { get; init; }
}

public class PlatformStateHandler(ITenantService tenantService, ILoggerFactory loggerFactory) : IMonoLambdaEventHandler
{
    public string EventType => "platform_state";

    private readonly ILogger<PlatformStateHandler> _logger = loggerFactory.CreateLogger<PlatformStateHandler>();
    
    public async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var header = message.Deserialize<Header>();
        if (header == null)
        {
            throw new Exception("Failed to parse platform state headers");
        }
        
        // Compare received version vs supported version.
        if (header.PayloadVersion != TenantDataVersion.Version)
        {
            // Mismatch doesn't always mean it won't work, but its worth warning.
            _logger.LogWarning("Platform State payload version mismatch got: {ReceivedVersion} wanted {CurrentVersion}. Consider regenerating the C# classes.", header.PayloadVersion, TenantDataVersion.Version);
        }
        
        if (!message.TryGetProperty("payload", out var payload))
        {
            throw new Exception("Platform state payload is missing a payload or was not a string");
        }

        // Check if payload is compressed
        var state = header.Compression switch
        {
            null   => payload.Deserialize<PlatformStatePayload>(),
            "gzip" => await DecompressAndDeserialize<PlatformStatePayload>(payload.GetString() ?? ""),
            _      => throw new Exception($"Unsupported compression {header.Compression}")
        };

        if (state == null)
        {
            throw new Exception("Platform State payload was null!");
        }
        
        _logger.LogInformation("Payload model version {Version}", header.PayloadVersion);
        _logger.LogInformation("tf-svc-infra serial {Serial}", state.TerraformSerials.Tfsvcinfra);
        _logger.LogInformation("Grafana serial {Serial}", state.TerraformSerials.Tfgrafana);
        _logger.LogInformation("Opensearch serial {Serial}", state.TerraformSerials.Tfopensearch);
        _logger.LogInformation("VanityUrls serial {Serial}", state.TerraformSerials.Tfvanityurl);
        _logger.LogInformation("WAF serial {Serial}", state.TerraformSerials.Tfwaf);
        
        await tenantService.UpdateState(state, cancellationToken);
    }

    public static async Task<T> DecompressAndDeserialize<T>(string base64CompressedData) where T : new()
    {
        if (string.IsNullOrEmpty(base64CompressedData))
        {
            throw new ArgumentException("Base64 compressed data cannot be null or empty.", nameof(base64CompressedData));
        }

        var compressedBytes = Convert.FromBase64String(base64CompressedData);
        
        using var compressedStream = new MemoryStream(compressedBytes);
        await using var decompressedStream = new GZipStream(compressedStream, CompressionMode.Decompress);

        var result = await JsonSerializer.DeserializeAsync<T>(decompressedStream);
        if (result == null)
        {
            throw new JsonException("Deserialization resulted in a null object, check the JSON structure.");
        }
        return result;
    }
}