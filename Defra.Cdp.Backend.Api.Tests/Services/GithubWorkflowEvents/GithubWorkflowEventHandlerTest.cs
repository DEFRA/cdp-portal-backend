using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents;

public class GithubWorkflowEventHandlerTest
{
    private readonly IAppConfigVersionsService _appConfigVersionsService = Substitute.For<IAppConfigVersionsService>();
    private readonly IAppConfigsService _appConfigsService = Substitute.For<IAppConfigsService>();
    
    private GithubWorkflowEventHandler CreateHandler()
    {
        return new GithubWorkflowEventHandler(
            _appConfigVersionsService,
            _appConfigsService,
            ConsoleLogger.CreateLogger<GithubWorkflowEventHandler>());
    }

    [Fact]
    public async Task WillProcessAppConfigVersionEvent()
    {
        var eventHandler = CreateHandler();

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

        await eventHandler.Handle(eventType, messageBody, CancellationToken.None);


        await _appConfigVersionsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<AppConfigVersionPayload>>(e =>
                e.EventType == "app-config-version" && e.Payload.CommitSha == "abc123" &&
                e.Payload.Environment == "infra-dev"),
            Arg.Any<CancellationToken>());
        await _appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
    }
   
    [Fact]
    public async Task WillProcessAppConfigEventAndUpdateEntityWithCreatingStatus()
    {
        var eventHandler = CreateHandler();

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

        await _appConfigsService.Received(1).PersistEvent(
            Arg.Is<CommonEvent<AppConfigPayload>>(e =>
                e.EventType == "app-config" && e.Payload.Environment == "infra-dev" &&
                e.Payload.Entities.Count == 2),
            Arg.Any<CancellationToken>());
        await _appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnrecognizedGithubWorkflowEvent()
    {
        var eventHandler = CreateHandler();

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

        await _appConfigVersionsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigVersionPayload>>(), Arg.Any<CancellationToken>());
        await _appConfigsService.DidNotReceive()
            .PersistEvent(Arg.Any<CommonEvent<AppConfigPayload>>(), Arg.Any<CancellationToken>());
    }
}