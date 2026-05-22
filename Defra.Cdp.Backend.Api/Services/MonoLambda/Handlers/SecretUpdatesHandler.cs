using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.Secrets.events;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;

/**
 {
  "event_type": "get_all_secret_keys",
  "get_all_secret_keys": true,
  "secretKeys": {
    "cdp/services/my-service": {
      "keys": ["KEY_ONE", "KEY_TWO"],
      "lastChangedDate": "2024-01-01T12:00:00+00:00",
      "createdDate": "2024-01-01T10:00:00+00:00"
    }   
  },  
  "exception": "", 
  "environment": "infra-dev",
  "timestamp": "2024-01-01T12:00:00+00:00"
}
 */

public class SecretUpdatesHandler(ISecretsService secretsService, ILogger<SecretUpdatesHandler> logger) : IMonoLambdaEventHandler
{
    public string EventType => "";
    public bool PersistEvents => false;
    public async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var body = message.Deserialize<BodyGetAllSecretKeys>();
        if (body == null)
        {
            logger.LogInformation("Failed to parse body of 'get_all_secret_keys' message");
            return;
        }

        if (body.Exception != "")
        {
            logger.LogError("get_all_secret_keys message contained exception {}", body.Exception);
            return;
        }

        logger.LogInformation("Get All Secrets: Updating secrets in {Environment}", body.Environment);
        var secrets = new List<TenantSecrets>();

        foreach (var (key, value) in body.SecretKeys)
        {
            var service = key.Replace("cdp/services/", "");
            secrets.Add(new TenantSecrets
            {
                Service = service,
                Environment = body.Environment,
                Keys = value.Keys,
                LastChangedDate = value.LastChangedDate,
                CreatedDate = value.CreatedDate
            });
        }

        var secretsInDb = await secretsService.FindAllSecretsForEnvironment(body.Environment, cancellationToken);

        var secretsToDelete = secretsInDb.ExceptBy(secrets.Select(s => s.Service),
            s => s.Service).ToList();

        if (secretsToDelete.Count != 0)
        {
            logger.LogInformation("Deleting {Count} secrets", secretsToDelete.Count);
            await secretsService.DeleteSecrets(secretsToDelete, cancellationToken);
        }

        await secretsService.UpdateSecrets(secrets, cancellationToken);
        logger.LogInformation("Get All Secrets: Updated secrets for {Environment}", body.Environment);
    }
}