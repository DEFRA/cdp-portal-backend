using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents;

public class GithubWorkflowEventHandlerTest
{
    private readonly IAppConfigVersionsService appConfigVersionsService = Substitute.For<IAppConfigVersionsService>();
    private readonly INginxVanityUrlsService vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
    private readonly ISquidProxyConfigService squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
    private readonly ITenantBucketsService tenantBucketsService = Substitute.For<ITenantBucketsService>();
    private readonly ITenantServicesService tenantServicesService = Substitute.For<ITenantServicesService>();
    private readonly IShutteredUrlsService shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
    private readonly IEnabledVanityUrlsService enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
    private readonly IEnabledApisService enabledApisService = Substitute.For<IEnabledApisService>();
    private readonly ITfVanityUrlsService tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
    private readonly IGrafanaDashboardsService grafanaDashboardsService = Substitute.For<IGrafanaDashboardsService>();
    private readonly IAppConfigsService appConfigsService = Substitute.For<IAppConfigsService>();
    private readonly INginxUpstreamsService nginxUpstreamsService = Substitute.For<INginxUpstreamsService>();
    private readonly IEntityStatusService entityStatusService = Substitute.For<IEntityStatusService>();

    private GithubWorkflowEventHandler createHandler()
    {
        return new GithubWorkflowEventHandler(
            appConfigVersionsService,
            appConfigsService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            enabledApisService,
            tfVanityUrlsService,
            grafanaDashboardsService,
            nginxUpstreamsService,
            entityStatusService,
            ConsoleLogger.CreateLogger<GithubWorkflowEventHandler>());
    }

    [Fact]
    public async Task WillProcessAppConfigVersionEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "app-config-version" };
        var messageBody =
            """
            {
              "eventType": "app-config-version",
              "timestamp": "2024-10-23T15:10:10.123",
              "payload": {
                "commitSha": "abc123",
                "commitTimestamp": "2024-10-23T15:10:10.123",
                "environment": "infra-dev"
              }
            }
            """;

        await eventHandler.Handle(eventType, messageBody, new CancellationToken());


        await appConfigVersionsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<AppConfigVersionPayload>>(e =>
                e.EventType == "app-config-version" && e.Payload.CommitSha == "abc123" &&
                e.Payload.Environment == "infra-dev"),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessVanityUrlsEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "nginx-vanity-urls" };
        var messageBody =
            """
            { "eventType": "nginx-vanity-urls",
              "timestamp": "2024-10-23T15:10:10.123",
              "payload": {
                "environment": "test",
                "services": [
                  {"name": "service-a", "urls": [{"domain": "service.gov.uk", "host": "service-a"}]},
                  {"name": "service-b", "urls": [{"domain": "service.gov.uk", "host": "service-b"}]}
                ]
              }
            }
            """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await vanityUrlsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<NginxVanityUrlsPayload>>(e =>
                e.EventType == "nginx-vanity-urls" && e.Payload.Environment == "test" && e.Payload.Services.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessSquidProxyConfigEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "squid-proxy-config" };
        const string messageBody = """
                                   { "eventType": "squid-proxy-config",
                                     "timestamp": "2024-10-23T15:10:10.123",
                                     "payload": {
                                       "environment": "test",
                                       "default_domains": [".cdp-int.defra.cloud", ".amazonaws.com", "login.microsoftonline.com", "www.gov.uk"],
                                       "services": [
                                           {"name": "cdp-dotnet-tracing", "allowed_domains": []},
                                           {"name": "find-ffa-data-ingester", "allowed_domains": ["fcpaipocuksss.search.windows.net", "fcpaipocuksoai.openai.azure.com", "www.gov.uk"]},
                                           {"name": "phi-frontend", "allowed_domains": ["gd.eppo.int"]}
                                       ]
                                     }
                                   }
                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await squidProxyConfigService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<SquidProxyConfigPayload>>(e =>
                e.EventType == "squid-proxy-config" && e.Payload.Environment == "test" &&
                e.Payload.Services.Count == 3),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessShutteredUrlEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "shuttered-urls" };
        var messageBody =
            """{"eventType": "shuttered-urls", "timestamp": "2024-12-30T12:13:26.320415+00:00", "payload": {"environment": "prod", "urls": ["foo.bar"]}}""";

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await shutteredUrlsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<ShutteredUrlsPayload>>(e =>
                e.EventType == "shuttered-urls" && e.Payload.Environment == "prod" && e.Payload.Urls.Count == 1),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessEnabledUrlEvents()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "enabled-urls" };
        var messageBody =
            """{"eventType": "enabled-urls", "timestamp": "2024-12-30T12:13:26.320415+00:00", "payload": {"environment": "prod", "urls": [ { "service": "foo", "url": "foo.bar" } ]}}""";

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await enabledUrlsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<EnabledVanityUrlsPayload>>(e =>
                e.EventType == "enabled-urls" && e.Payload.Environment == "prod" && e.Payload.Urls.Count == 1),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessTenantBucketsEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "tenant-buckets" };
        const string messageBody = """
                                   {
                                     "eventType": "tenant-buckets",
                                     "timestamp": "2024-11-23T15:10:10.123123+00:00",
                                     "payload": {
                                       "environment": "test",
                                       "buckets": [
                                         {
                                           "name": "frontend-service-bucket",
                                           "exists": true,
                                           "services_with_access": [
                                             "frontend-service",
                                             "backend-service"
                                           ]
                                         },
                                         {
                                           "name": "backend-service-bucket",
                                           "exists": false,
                                           "services_with_access": [
                                             "backend-service"
                                           ]
                                         }
                                       ]
                                     }
                                   }

                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await tenantBucketsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<TenantBucketsPayload>>(e =>
                e.EventType == "tenant-buckets" && e.Payload.Environment == "test" && e.Payload.Buckets.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessTenantServicesEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "tenant-services" };
        const string messageBody = """
                                   {
                                     "eventType": "tenant-services",
                                     "timestamp": "2024-11-23T15:10:10.123123+00:00",
                                     "payload": {
                                       "environment": "test",
                                       "services": [
                                         {
                                           "zone": "public",
                                           "mongo": false,
                                           "redis": true,
                                           "service_code": "CDP",
                                           "test_suite": "frontend-service-tests",
                                           "name": "frontend-service",
                                           "buckets": [
                                             "frontend-service-buckets-*"
                                           ],
                                           "queues": [
                                             "frontend-service-queue"
                                           ]
                                         },
                                         {
                                           "zone": "protected",
                                           "mongo": true,
                                           "redis": false,
                                           "service_code": "CDP",
                                           "name": "backend-service",
                                           "rds_aurora_postgres": true
                                         }
                                       ]
                                     }
                                   }
                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await tenantServicesService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<TenantServicesPayload>>(e =>
                e.EventType == "tenant-services" && e.Payload.Environment == "test" && e.Payload.Services.Count == 2 &&
                e.Payload.Services[0].Postgres == false && e.Payload.Services[1].Postgres == true),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task WillProcessTfVanityUrlsEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "tf-vanity-urls" };
        const string messageBody = """
                                    {
                                        "eventType": "tf-vanity-urls",
                                        "timestamp": "2025-01-28T10:59:02.445635+00:00",
                                        "payload": {
                                            "environment": "ext-test",
                                            "vanity_urls":
                                                [
                                                    {
                                                        "public_url": "pha-import-notifications.integration.api.defra.gov.uk",
                                                        "service_name": "pha-import-notifications",
                                                        "enable_alb": false,
                                                        "enable_acm": true,
                                                        "is_api": false
                                                    }, {
                                                        "public_url": "pha-import-notifications-2.integration.api.defra.gov.uk",
                                                        "enable_alb": false,
                                                        "enable_acm": false
                                                    }
                                                ]
                                        }
                                    }
                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await tfVanityUrlsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<TfVanityUrlsPayload>>(e =>
                e.EventType == "tf-vanity-urls" && e.Payload.Environment == "ext-test" &&
                e.Payload.VanityUrls.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessEnabledApisEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "enabled-apis" };
        const string messageBody = """
                                    {
                                        "eventType": "enabled-apis",
                                        "timestamp": "2025-02-02T10:59:02.445635+00:00",
                                        "payload": {
                                            "environment": "ext-test",
                                            "apis":
                                                [
                                                   {
                                                       "service": "pha-import-notifications",
                                                       "api": "pha-import-notifications.integration.api.defra.gov.uk"
                                                   }
                                                ]
                                        }
                                    }
                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await enabledApisService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<EnabledApisPayload>>(e =>
                e.EventType == "enabled-apis" && e.Payload.Environment == "ext-test" &&
                e.Payload.Apis.Count == 1),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessAppConfigEventAndUpdateEntityWithCreatingStatus()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "app-config" };
        const string messageBody = """
                                   {
                                     "eventType": "app-config",
                                     "timestamp": "2024-10-23T15:10:10.123",
                                     "payload": {
                                       "commitSha": "abc123",
                                       "commitTimestamp": "2024-10-23T15:10:10.123",
                                       "environment": "infra-dev",
                                       "entities": ["service-1", "service-2"]
                                     }
                                   }
                                   """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await appConfigsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<AppConfigPayload>>(e =>
                e.EventType == "app-config" && e.Payload.Environment == "infra-dev" &&
                e.Payload.Entities.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tfVanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TfVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnrecognizedGithubWorkflowEvent()
    {
        var eventHandler = createHandler();

        var eventType = new CommonEventWrapper { EventType = "unrecognized-github-workflow-event" };
        var messageBody =
            """
            { "eventType": "unrecognized-github-workflow-event",
              "timestamp": "2024-10-23T15:10:10.123",
              "payload": {
                "environment": "test"
              }
            }
            """;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledApisService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<EnabledApisPayload>>(), Arg.Any<CancellationToken>());
        await appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
        await nginxUpstreamsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<NginxUpstreamsPayload>>(), Arg.Any<CancellationToken>());
        await grafanaDashboardsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<GrafanaDashboardPayload>>(), Arg.Any<CancellationToken>());
        await entityStatusService.Received(1)
            .UpdatePendingEntityStatuses(Arg.Any<CancellationToken>());
    }
}