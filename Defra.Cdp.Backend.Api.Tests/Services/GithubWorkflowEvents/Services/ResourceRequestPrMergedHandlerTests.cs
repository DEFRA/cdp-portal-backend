using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class ResourceRequestPrMergedHandlerTests
{
    private readonly IResourceRequestService _resourceRequestService = Substitute.For<IResourceRequestService>();
    
    [Fact]
    public async Task Should_update_status_to_merged()
    {
        _resourceRequestService.UpdatePullRequestStatus(
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResourceRequestRecord
            {
                EntityName = "foo-backend",
                Entities = ["foo-backend"],
                Status = PrStatus.Merged,
                RequestedBy = new UserDetails { Id = "user-1", DisplayName = "Jane Doe" }
            });

        var handler = new ResourceRequestPrMergedHandler(
            _resourceRequestService,
            new NullLogger<ResourceRequestPrMergedHandler>());

        var msg = """
                  {
                    "eventType": "resource-request-pr-merged",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "prNumber": 42
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);

        await _resourceRequestService.Received(1).UpdatePullRequestStatus(
            42,
            Arg.Is(PrStatus.Merged),
            Arg.Any<CancellationToken>());
    }
}