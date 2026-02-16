using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Secrets.events;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface ISecretEventHandler
{
    Task Handle(SecretMessage message, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class SecretEventHandler(
    ISecretsService secretsService,
    IPendingSecretsService pendingSecretsService,
    ILogger<SecretEventHandler> logger)
    : ISecretEventHandler
{
    public async Task Handle(SecretMessage message, CancellationToken cancellationToken)
    {
        switch (message.Action)
        {
            case "get_all_secret_keys":
                await HandleGetAllSecrets(message, cancellationToken);
                break;
            case "add_secret":
                await HandleAddSecret(message, cancellationToken);
                break;
            case "remove_secret_by_key":
                await HandleRemoveSecret(message, cancellationToken);
                break;
            default:
                logger.LogDebug("Ignoring action: {Action} not handled", message.Action);
                return;
        }
    }

    /**
     * Handler for get_all_secret_keys action. Contains a dict of all the services in an environment that have
     * secret values set along with a list of the key/environment variable the secret is bound to,
     * but NOT the actual secret itself.
     */
    private async Task HandleGetAllSecrets(SecretMessage message, CancellationToken cancellationToken)
    {
        var body = message.Body?.Deserialize<BodyGetAllSecretKeys>();
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

        logger.LogInformation("Get All Secrets: Processing {Action}", message.Action);
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

        if (secrets.Count != 0)
        {
            await secretsService.UpdateSecrets(secrets, cancellationToken);
        }

        await secretsService.UpdateSecrets(secrets, cancellationToken);
        logger.LogInformation("Get All Secrets: Updated secrets for {Environment}", body.Environment);
    }

    /**
     * Handler for add_secret action. If the add_secret request matches a pending secret then move the pending secret
     * to the tenant secrets collection.
     */
    private async Task HandleAddSecret(SecretMessage message, CancellationToken cancellationToken)
    {
        var body = message.Body?.Deserialize<BodyAddRemoveSecret>();
        if (body == null)
        {
            logger.LogInformation("Add Secret: Failed to parse body of 'add_secret' message");
            return;
        }

        logger.LogInformation("Add Secret: Processing {Action}", message.Action);
        var service = body.SecretName.Replace("cdp/services/", "");

        if (body.Exception != "")
        {
            await pendingSecretsService.AddException(
                body.Environment, service, body.SecretKey, "add_secret", body.Exception, cancellationToken);
            logger.LogError("Add Secret: add_secret message contained exception {Exception}", body.Exception);
            return;
        }

        var pendingSecret = await pendingSecretsService.ExtractPendingSecret(body.Environment, service,
            body.SecretKey, "add_secret", cancellationToken);

        if (pendingSecret != null)
        {
            await secretsService.AddSecretKey(body.Environment, service, pendingSecret.SecretKey, cancellationToken);

            logger.LogInformation("Add Secret: Added pending secret {SecretKey} in {Environment} to {Service}",
                pendingSecret
                    .SecretKey, body.Environment, service);
        }
        else
        {
            logger.LogInformation(
                "Add Secret: Secret {SecretKey} not found in pending secrets for {Service} in {Environment}",
                body.SecretKey, service, body.Environment);
        }
    }

    private async Task HandleRemoveSecret(SecretMessage message, CancellationToken cancellationToken)
    {
        var body = message.Body?.Deserialize<BodyAddRemoveSecret>();
        if (body == null)
        {
            logger.LogInformation("Remove Secret: Failed to parse body of 'remove_secret_by_key' message");
            return;
        }

        logger.LogInformation("Remove Secret: Processing {Action}", message.Action);
        var service = body.SecretName.Replace("cdp/services/", "");

        if (body.Exception != "")
        {
            await pendingSecretsService.AddException(
                body.Environment, service, body.SecretKey, "remove_secret_by_key", body.Exception, cancellationToken);
            logger.LogError("Remove Secret: remove_secret_by_key message contained exception {Exception}",
                body.Exception);
            return;
        }

        var pendingSecret = await pendingSecretsService.ExtractPendingSecret(body.Environment, service,
            body.SecretKey, "remove_secret_by_key", cancellationToken);

        if (pendingSecret != null)
        {
            await secretsService.RemoveSecretKey(body.Environment, service, pendingSecret.SecretKey, cancellationToken);

            logger.LogInformation("Remove Secret: Removed pending secret {SecretKey} in {Environment} to {Service}",
                pendingSecret
                    .SecretKey, body.Environment, service);
        }
        else
        {
            logger.LogInformation(
                "Remove Secret: Secret {SecretKey} not found in pending secrets for {Service} in {Environment}",
                body.SecretKey, service, body.Environment);
        }
    }

    public static SecretMessage? TryParseMessage(string body)
    {
        try
        {
            var header = JsonSerializer.Deserialize<SecretMessage>(body);
            return header?.Source != "cdp-secret-manager-lambda" ? null : header;
        }
        catch (Exception)
        {
            return null;
        }
    }
}