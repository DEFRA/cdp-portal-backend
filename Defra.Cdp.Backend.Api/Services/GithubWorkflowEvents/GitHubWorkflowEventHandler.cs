using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

public interface IGithubWorkflowEventHandler
{
    Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles specific payloads sent by the secret manager lambda.
 * All messages have the same outer body detailing the source & action.
 */
public class GithubWorkflowEventHandler(
    IAppConfigVersionsService appConfigVersionsService,
    IAppConfigsService appConfigsService,
    INginxVanityUrlsService nginxVanityUrlsService,
    ISquidProxyConfigService squidProxyConfigService,
    ITenantBucketsService tenantBucketsService,
    ITenantServicesService tenantServicesService,
    IShutteredUrlsService shutteredUrlsService,
    IEnabledVanityUrlsService enabledVanityUrlsService,
    IEnabledApisService enabledApisService,
    ITfVanityUrlsService tfVanityUrlsService,
    IGrafanaDashboardsService grafanaDashboardsService,
    INginxUpstreamsService nginxUpstreamsService,
    IEntityStatusService entityStatusService,
    ITenantRdsDatabasesService tenantRdsService,
    ILogger<GithubWorkflowEventHandler> logger)
    : IGithubWorkflowEventHandler
{
    public async Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken)
    {
        switch (eventWrapper.EventType)
        {
            case "app-config-version":
                await HandleEvent(eventWrapper, messageBody, appConfigVersionsService, cancellationToken);
                break;
            case "app-config":
                await HandleEvent(eventWrapper, messageBody, appConfigsService, cancellationToken);
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
            case "tenant-rds":
                await HandleEvent(eventWrapper, messageBody, tenantRdsService, cancellationToken);
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
            case "grafana-dashboard":
                await HandleEvent(eventWrapper, messageBody, grafanaDashboardsService, cancellationToken);
                break;
            case "nginx-upstreams":
                await HandleEvent(eventWrapper, messageBody, nginxUpstreamsService, cancellationToken);
                break;
            default:
                logger.LogInformation("Ignoring event: {EventType} not handled {Message}", eventWrapper.EventType, messageBody);
                break;
        }
        await entityStatusService.UpdatePendingEntityStatuses(cancellationToken);
    }

    private async Task HandleEvent<T>(CommonEventWrapper eventWrapper, string messageBody, IEventsPersistenceService<T> service,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling event: {EventType}", eventWrapper.EventType);
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<T>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogInformation("Failed to parse Github workflow event - message: {MessageBody}", messageBody);
            return;
        }

        await service.PersistEvent(workflowEvent, cancellationToken);
    }
}