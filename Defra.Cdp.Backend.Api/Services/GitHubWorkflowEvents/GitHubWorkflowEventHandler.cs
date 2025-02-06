using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents;

public interface IGitHubWorkflowEventHandler
{
    Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class GitHubWorkflowEventHandler(
    IAppConfigVersionsService appConfigVersionsService,
    INginxVanityUrlsService nginxVanityUrlsService,
    ISquidProxyConfigService squidProxyConfigService,
    ITenantBucketsService tenantBucketsService,
    ITenantServicesService tenantServicesService,
    IShutteredUrlsService shutteredUrlsService,
    IEnabledVanityUrlsService enabledVanityUrlsService,
    IEnabledApisService enabledApisService,
    ITfVanityUrlsService tfVanityUrlsService,
    ILogger<GitHubWorkflowEventHandler> logger)
    : IGitHubWorkflowEventHandler
{
    public async Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken)
    {
        switch (eventWrapper.EventType)
        {
            case "app-config-version":
                await HandleEvent(eventWrapper, messageBody, appConfigVersionsService, cancellationToken);
                break;
            case "nginx-vanity-urls":
                await HandleEvent(eventWrapper, messageBody, nginxVanityUrlsService, cancellationToken);
                break;
            case "squid-proxy-config":
                await HandleEvent(eventWrapper, messageBody, squidProxyConfigService, cancellationToken);
                break;
            case "tenant-buckets":
                await HandleEvent(eventWrapper, messageBody, tenantBucketsService, cancellationToken);
                break;
            case "tenant-services":
                await HandleEvent(eventWrapper, messageBody, tenantServicesService, cancellationToken);
                break;
            case "shuttered-urls":
                await HandleEvent(eventWrapper, messageBody, shutteredUrlsService, cancellationToken);
                break;
            case "enabled-urls":
                await HandleEvent(eventWrapper, messageBody, enabledVanityUrlsService, cancellationToken);
                break;
            case "enabled-apis":
                await HandleEvent(eventWrapper, messageBody, enabledApisService, cancellationToken);
                break;
            case "tf-vanity-urls":
                await HandleEvent(eventWrapper, messageBody, tfVanityUrlsService, cancellationToken);
                break;
            default:
                 logger.LogInformation("Ignoring event: {EventType} not handled {Message}", eventWrapper.EventType, messageBody);
                 break;
        }
    }

    private async Task HandleEvent<T>(CommonEventWrapper eventWrapper, string messageBody, IEventsPersistenceService<T> service,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling event: {EventType}", eventWrapper.EventType);
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<T>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogInformation("Failed to parse GitHub workflow event - message: {MessageBody}", messageBody);
            return;
        }

        await service.PersistEvent(workflowEvent, cancellationToken);
    }
}
