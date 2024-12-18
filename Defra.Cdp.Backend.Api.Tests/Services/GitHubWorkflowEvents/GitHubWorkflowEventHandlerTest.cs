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
        var vanityUrlsService = Substitute.For<IVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var eventHandler = new GitHubWorkflowEventHandler(
            appConfigVersionService, 
            vanityUrlsService,
            squidProxyConfigService,
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


        await appConfigVersionService.PersistEvent(
            Arg.Is<Event<AppConfigVersionPayload>>(e =>
                e.EventType == "app-config-version" && e.Payload.CommitSha == "abc123" &&
                e.Payload.Environment == "infra-dev"),
            Arg.Any<CancellationToken>());
        await vanityUrlsService.Received(0)
            .PersistEvent(Arg.Any<Event<VanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.Received(0)
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessVanityUrlsEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<IVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
            squidProxyConfigService,
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

        await vanityUrlsService.PersistEvent(
            Arg.Is<Event<VanityUrlsPayload>>(e =>
                e.EventType == "nginx-vanity-urls" && e.Payload.Environment == "test" && e.Payload.Services.Count == 2),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.Received(0)
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await squidProxyConfigService.Received(0)
            .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task WillProcessSquidProxyConfigEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<IVanityUrlsService>();
        var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
        var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
            squidProxyConfigService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventWrapper { EventType = "squid-proxy-config" };
        var messageBody =
            """
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

        await squidProxyConfigService.PersistEvent(
            Arg.Is<Event<SquidProxyConfigPayload>>(e =>
                e.EventType == "squid-proxy-config" && e.Payload.Environment == "test" && e.Payload.Services.Count == 3),
            Arg.Any<CancellationToken>());
        await appConfigVersionService.Received(0)
            .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await vanityUrlsService.Received(0)
            .PersistEvent(Arg.Any<Event<VanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnrecognizedGitHubWorkflowEvent()
    {
        {
            var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
            var vanityUrlsService = Substitute.For<IVanityUrlsService>();
            var squidProxyConfigService = Substitute.For<ISquidProxyConfigService>();
            var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
                squidProxyConfigService,
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

            await appConfigVersionService.Received(0)
                .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
            await vanityUrlsService.Received(0)
                .PersistEvent(Arg.Any<Event<VanityUrlsPayload>>(), Arg.Any<CancellationToken>());
            await squidProxyConfigService.Received(0)
                .PersistEvent(Arg.Any<Event<SquidProxyConfigPayload>>(), Arg.Any<CancellationToken>());
        }
    }
}