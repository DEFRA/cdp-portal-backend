using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GitHubWorkflowEvents;

public class GitHubWorkflowEventHandlerTest
{
    [Fact]
    public async Task WillProcessAppConfigVersionEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "app-config-version" };
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


        await appConfigVersionService.Received(1).PersistEvent(
            Arg.Is<Event<AppConfigVersionPayload>>(e =>
                e.EventType == "app-config-version" && e.Payload.CommitSha == "abc123" &&
                e.Payload.Environment == "infra-dev"),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessVanityUrlsEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "nginx-vanity-urls" };
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

        await eventHandler.Handle(eventType, messageBody, new CancellationToken());

        await vanityUrlsService.Received(1).PersistEvent(
            Arg.Is<Event<NginxVanityUrlsPayload>>(e =>
                e.EventType == "nginx-vanity-urls" && e.Payload.Environment == "test" && e.Payload.Services.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessSquidProxyConfigEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "squid-proxy-config" };
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
            Arg.Is<Event<SquidProxyConfigPayload>>(e =>
                e.EventType == "squid-proxy-config" && e.Payload.Environment == "test" &&
                e.Payload.Services.Count == 3),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessShutteredUrlEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "shuttered-urls" };
        var messageBody =
            """{"eventType": "shuttered-urls", "timestamp": "2024-12-30T12:13:26.320415+00:00", "payload": {"environment": "prod", "urls": ["foo.bar"]}}""";

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await shutteredUrlsService.Received(1).PersistEvent(
            Arg.Is<Event<ShutteredUrlsPayload>>(e =>
                e.EventType == "shuttered-urls" && e.Payload.Environment == "prod" && e.Payload.Urls.Count == 1),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessEnabledUrlEvents()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "enabled-urls" };
        var messageBody =
            """{"eventType": "enabled-urls", "timestamp": "2024-12-30T12:13:26.320415+00:00", "payload": {"environment": "prod", "urls": [ { "service": "foo", "url": "foo.bar" } ]}}""";

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await enabledUrlsService.Received(1).PersistEvent(
            Arg.Is<Event<EnabledVanityUrlsPayload>>(e =>
                e.EventType == "enabled-urls" && e.Payload.Environment == "prod" && e.Payload.Urls.Count == 1),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessTenantBucketsEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "tenant-buckets" };
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
            Arg.Is<Event<TenantBucketsPayload>>(e =>
                e.EventType == "tenant-buckets" && e.Payload.Environment == "test" && e.Payload.Buckets.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessTenantServicesEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "tenant-services" };
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
                                           "name": "backend-service"
                                         }
                                       ]
                                     }
                                   }
                                   """;
        ;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await tenantServicesService.Received(1).PersistEvent(
            Arg.Is<Event<TenantServicesPayload>>(e =>
                e.EventType == "tenant-services" && e.Payload.Environment == "test" && e.Payload.Services.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessTfVanityUrlsEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "tf-vanity-urls" };
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
                                                        "service_name": "pha-import-notifications",
                                                        "enable_alb": false, 
                                                        "enable_acm": false, 
                                                        "is_api": false
                                                    }
                                                ]
                                        }
                                    }
                                   """;
        ;

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

        await tfVanityUrlsService.Received(1).PersistEvent(
            Arg.Is<Event<TfVanityUrlsPayload>>(e =>
                e.EventType == "tf-vanity-urls" && e.Payload.Environment == "ext-test" && e.Payload.VanityUrls.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnrecognizedGitHubWorkflowEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<INginxVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var tenantBucketsService = Substitute.For<ITenantBucketsService>();
        var tenantServicesService = Substitute.For<ITenantServicesService>();
        var shutteredUrlsService = Substitute.For<IShutteredUrlsService>();
        var enabledUrlsService = Substitute.For<IEnabledVanityUrlsService>();
        var tfVanityUrlsService = Substitute.For<ITfVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService,
            vanityUrlsService,
            squidProxyConfigService,
            tenantBucketsService,
            tenantServicesService,
            shutteredUrlsService,
            enabledUrlsService,
            tfVanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "unrecognized-github-workflow-event" };
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

        await appConfigVersionService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<NginxVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        await tenantBucketsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantBucketsPayload>>(), Arg.Any<CancellationToken>());
        await tenantServicesService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<TenantServicesPayload>>(), Arg.Any<CancellationToken>());
        await shutteredUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<ShutteredUrlsPayload>>(), Arg.Any<CancellationToken>());
        await enabledUrlsService.DidNotReceive()
            .PersistEvent(Arg.Any<Event<EnabledVanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }
}