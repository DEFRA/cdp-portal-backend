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
        var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventType { EventType = "app-config-version" };
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
        await vanityUrlsService.Received(0).PersistEvent(Arg.Any<Event<VanityUrlsPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WillProcessVanityUrlsEvent()
    {
        var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
        var vanityUrlsService = Substitute.For<IVanityUrlsService>();
        var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
            ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

        var eventType = new GitHubWorkflowEventType { EventType = "nginx-vanity-urls" };
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
    }


    [Fact]
    public async Task UnrecognizedGitHubWorkflowEvent()
    {
        {
            var appConfigVersionService = Substitute.For<IAppConfigVersionService>();
            var vanityUrlsService = Substitute.For<IVanityUrlsService>();
            var eventHandler = new GitHubWorkflowEventHandler(appConfigVersionService, vanityUrlsService,
                ConsoleLogger.CreateLogger<GitHubWorkflowEventHandler>());

            var eventType = new GitHubWorkflowEventType { EventType = "unrecognized-github-workflow-event" };
            var messageBody =
                """
                { "eventType": "unrecognized-github-workflow-event",
                  "timestamp": "2024-10-23T15:10:10.123",
                  "payload": {
                    "environment": "test"
                  }
                }
                """;

            await eventHandler.Handle(eventType, messageBody, new CancellationToken());

            await appConfigVersionService.Received(0)
                .PersistEvent(Arg.Any<Event<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
            await vanityUrlsService.Received(0).PersistEvent(Arg.Any<Event<VanityUrlsPayload>>(), Arg.Any<CancellationToken>());
        }
    }
}
