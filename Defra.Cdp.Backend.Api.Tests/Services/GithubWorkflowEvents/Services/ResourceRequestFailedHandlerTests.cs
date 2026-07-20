using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class ResourceRequestFailedHandlerTests
{
    private readonly IResourceRequestService _resourceRequestService = Substitute.For<IResourceRequestService>();

    [Fact]
    public async Task Should_mark_status_as_failed()
    {
        _resourceRequestService.MarkFailed(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResourceRequestRecord
            {
                EntityName = "foo-backend",
                Entities = ["foo-backend"],
                Status = PrStatus.Failed,
                RequestedBy = new UserDetails { Id = "user-1", DisplayName = "Jane Doe" }
            });

        var handler = new ResourceRequestFailedHandler(
            _resourceRequestService,
            new NullLogger<ResourceRequestFailedHandler>());

        var msg = """
                  {
                    "eventType": "resource-request-failed",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "runId": "run-123",
                        "workflowRunId": "123456789",
                        "workflowRunUrl": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/123456789"
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _resourceRequestService.Received(1).MarkFailed(
            "run-123",
            Arg.Any<CancellationToken>());
    }
}
