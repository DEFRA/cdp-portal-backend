using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class ResourceRequestPrEventHandlerTest
{
    private readonly IResourceRequestService _resourceRequestService = Substitute.For<IResourceRequestService>();

    [Fact]
    public async Task Should_attach_pull_request_when_message_is_valid()
    {
        _resourceRequestService.AttachPullRequest(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<ResourceRequestPullRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ResourceRequestPrEventHandler(_resourceRequestService, new NullLogger<ResourceRequestPrEventHandler>());

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
            "tenant-request-run-123",
            Arg.Is<ResourceRequestPullRequest>(pr => pr.Url == "https://github.com/DEFRA/cdp-tenant-config/pull/99" && pr.Number == 99),
            Arg.Any<CancellationToken>());
    }
}
