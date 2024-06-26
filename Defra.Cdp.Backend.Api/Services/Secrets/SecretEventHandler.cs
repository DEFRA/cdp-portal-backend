using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets.events;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface ISecretEventHandler
{
   Task Handle(MessageHeader header, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class SecretEventHandler : ISecretEventHandler
{
    
    private readonly ISecretsService _secretsService;
    private readonly ILogger<SecretEventHandler> _logger;
    
    public SecretEventHandler(ISecretsService secretsService, ILogger<SecretEventHandler> logger)
    {
        _logger = logger;
        _secretsService = secretsService;
    }

    public async Task Handle(MessageHeader header, CancellationToken cancellationToken)
    {
        switch (header.Action)
        {
            case "get_all_secret_keys":
                await HandleGetAllSecrets(header, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignoring action: {Action} not handled", header.Action);
                return;
        }
    }

    /**
     * Handler for get_all_secret_keys action. Contains a dict of all the services in an environment that have
     * secret values set along with a list of the key/environment variable the secret is bound to,
     * but NOT the actual secret itself.
     */
    public async Task HandleGetAllSecrets(MessageHeader header, CancellationToken cancellationToken)
    {
        var body = header.Body?.Deserialize<BodyGetAllSecretKeys>();
        if (body == null)
        {
            _logger.LogInformation("Failed to parse body of 'get_all_secret_keys' message");   
            return;
        }

        if (body.Exception != "")
        {
            _logger.LogError("get_all_secret_keys message contained exception {}", body.Exception);
            return;
        } 
        
        _logger.LogInformation("Updating secrets in {Environment}", body.Environment);
        var secrets = new List<TenantSecrets>();
        
        foreach (var kv in body.Keys)
        {
            var service = kv.Key.Replace("cdp/services/", "");
            secrets.Add(new TenantSecrets
            {
                Service = service, Environment = body.Environment, Secrets = kv.Value
            });
        }
        
        await _secretsService.UpdateSecrets(secrets, cancellationToken);
        _logger.LogInformation("Updated secrets for {Environment}", body.Environment);
    }
    
    public static MessageHeader? TryParseMessageHeader(string body)
    {
        try
        {
            var header = JsonSerializer.Deserialize<MessageHeader>(body);
            return header?.Source != "cdp-secret-manager-lambda" ? null : header;
        }
        catch(Exception e)
        {
            return null;
        }
    }
}