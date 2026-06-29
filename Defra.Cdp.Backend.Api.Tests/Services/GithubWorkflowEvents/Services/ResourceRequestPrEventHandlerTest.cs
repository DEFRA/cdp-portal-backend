using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class ResourceRequestPrEventHandlerTest
{
    private static readonly string TestChannel = "cdp-portal-alerts";

    private readonly IResourceRequestService _resourceRequestService = Substitute.For<IResourceRequestService>();
    private readonly ISlackClient _slackClient = Substitute.For<ISlackClient>();

    private static IConfiguration ConfigWithChannel(string channel) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantResourceRequested:SlackChannel"] = channel
            })
            .Build();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    [Fact]
    public async Task Should_attach_pull_request_and_send_slack_notification_when_request_is_found()
    {
        _resourceRequestService.AttachPullRequest(
                Arg.Any<string>(),
                Arg.Any<ResourceRequestPullRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResourceRequestRecord
            {
                EntityName = "foo-backend",
                RequestedBy = new UserDetails { Id = "user-1", DisplayName = "Jane Doe" }
            });

        var handler = new ResourceRequestPrEventHandler(
            _resourceRequestService,
            _slackClient,
            ConfigWithChannel(TestChannel),
            new NullLogger<ResourceRequestPrEventHandler>());

        var msg = """
                  {
                    "eventType": "resource-request-pr",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "runId": "run-123",
                        "workflowRunId": "123456789",
                        "workflowRunUrl": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/123456789",
                        "repository": "DEFRA/cdp-tenant-config",
                        "branch": "tenant-request-run-123",
                        "prUrl": "https://github.com/DEFRA/cdp-tenant-config/pull/99",
                        "prNumber": 99
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _resourceRequestService.Received(1).AttachPullRequest(
            "run-123",
            Arg.Is<ResourceRequestPullRequest>(pr =>
                pr.Url == "https://github.com/DEFRA/cdp-tenant-config/pull/99" && pr.Number == 99),
            Arg.Any<CancellationToken>());

        await _slackClient.Received(1).SendToChannel(
            TestChannel,
            Arg.Any<SlackMessageBody>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_send_slack_notification_when_run_id_is_not_found()
    {
        _resourceRequestService.AttachPullRequest(
                Arg.Any<string>(),
                Arg.Any<ResourceRequestPullRequest>(),
                Arg.Any<CancellationToken>())
            .Returns((ResourceRequestRecord?)null);

        var handler = new ResourceRequestPrEventHandler(
            _resourceRequestService,
            _slackClient,
            ConfigWithChannel(TestChannel),
            new NullLogger<ResourceRequestPrEventHandler>());

        var msg = """
                  {
                    "eventType": "resource-request-pr",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "runId": "missing-run",
                        "workflowRunId": "123456789",
                        "workflowRunUrl": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/123456789",
                        "repository": "DEFRA/cdp-tenant-config",
                        "branch": "tenant-request-missing-run",
                        "prUrl": "https://github.com/DEFRA/cdp-tenant-config/pull/100",
                        "prNumber": 100
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _slackClient.DidNotReceiveWithAnyArgs().SendToChannel(
            Arg.Any<string>(), Arg.Any<SlackMessageBody>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_send_slack_notification_when_channel_is_not_configured()
    {
        _resourceRequestService.AttachPullRequest(
                Arg.Any<string>(),
                Arg.Any<ResourceRequestPullRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResourceRequestRecord
            {
                EntityName = "foo-backend",
                RequestedBy = new UserDetails { Id = "user-1", DisplayName = "Jane Doe" }
            });

        var handler = new ResourceRequestPrEventHandler(
            _resourceRequestService,
            _slackClient,
            EmptyConfig(),
            new NullLogger<ResourceRequestPrEventHandler>());

        var msg = """
                  {
                    "eventType": "resource-request-pr",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "runId": "run-123",
                        "workflowRunId": "123456789",
                        "workflowRunUrl": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/123456789",
                        "repository": "DEFRA/cdp-tenant-config",
                        "branch": "tenant-request-run-123",
                        "prUrl": "https://github.com/DEFRA/cdp-tenant-config/pull/99",
                        "prNumber": 99
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _slackClient.DidNotReceiveWithAnyArgs().SendToChannel(
            Arg.Any<string>(), Arg.Any<SlackMessageBody>(), Arg.Any<CancellationToken>());
    }
}
