using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SimpleNotificationService;

namespace Defra.Cdp.Backend.Api.Utils.Sns;

public class SecretsSnsMessageHandler(AmazonSimpleNotificationServiceClient client, string topic)
{
    public async Task Send(AddSecret message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message);
        await client.PublishAsync(topic, payload, cancellationToken);
    }

    public async Task Send(RemoveSecret message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message);
        await client.PublishAsync(topic, payload, cancellationToken);
    }
}

public class AddSecret
{
    [JsonPropertyName("environment")]
    public required string Environment { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
    
    [JsonPropertyName("secret_key")]
    public required string SecretKey { get; init; }
    
    [JsonPropertyName("secret_value")]
    public required string SecretValue { get; init; }

    [JsonPropertyName("action")]
    public string Action => "add_secret";
}


public class RemoveSecret
{
    [JsonPropertyName("environment")]
    public string Environment { get; init; }
    
    [JsonPropertyName("name")]
    public string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
    
    [JsonPropertyName("secret_key")]
    public required string SecretKey { get; init; }
    
    [JsonPropertyName("action")]
    public string Action => "remove_secret_by_key";
}