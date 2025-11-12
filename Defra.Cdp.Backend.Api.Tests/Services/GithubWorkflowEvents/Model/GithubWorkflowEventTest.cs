using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Model;

public class GithubWorkflowEventTest
{
    [Fact]
    public void WillDeserializeAppConfigVersionEvent()
    {
        const string messageBody = """
                                   {
                                     "eventType": "app-config-version",
                                     "timestamp": "2024-11-23T15:10:10.123123+00:00",
                                     "payload": {
                                       "commitSha": "abc123",
                                       "commitTimestamp": "2024-11-23T15:10:10.123",
                                       "environment": "infra-dev"
                                     }
                                   }
                                   """;

        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<AppConfigVersionPayload>>(messageBody);

        Assert.Equal("app-config-version", workflowEvent?.EventType);
        Assert.Equal("infra-dev", workflowEvent?.Payload.Environment);
        Assert.Equal("abc123", workflowEvent?.Payload.CommitSha);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123), workflowEvent?.Payload.CommitTimestamp);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);
    }
}