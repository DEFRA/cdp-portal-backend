using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.GitHubWorkflowEvents.Model;

public class EventTest
{
    [Fact]
    public void WillDeserializeOldStyleAppConfigVersionEvent()
    {
        var messageBody = """
                          {
                            "action": "app-config-version",
                            "content": {
                              "commitSha": "abc123",
                              "commitTimestamp": "2024-10-23T15:10:10.123",
                              "environment": "infra-dev"
                            }
                          }
                          """;

        var workflowEvent = JsonSerializer.Deserialize<Event<AppConfigVersionPayload>>(messageBody);

        Assert.Equal("app-config-version", workflowEvent?.Action);
        Assert.Equal("app-config-version", workflowEvent?.EventType);

        Assert.Equal("infra-dev", workflowEvent?.Content?.Environment);
        Assert.Equal("infra-dev", workflowEvent?.Payload?.Environment);

        Assert.Equal("abc123", workflowEvent?.Content?.CommitSha);
        Assert.Equal("abc123", workflowEvent?.Payload?.CommitSha);

        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Content?.CommitTimestamp);
        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Payload?.CommitTimestamp);
    }

    [Fact]
    public void WillDeserializeAppConfigVersionEvent()
    {
        var messageBody = """
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

        var workflowEvent = JsonSerializer.Deserialize<Event<AppConfigVersionPayload>>(messageBody);

        Assert.Equal("app-config-version", workflowEvent?.EventType);
        Assert.Equal("infra-dev", workflowEvent?.Payload?.Environment);
        Assert.Equal("abc123", workflowEvent?.Payload?.CommitSha);
        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Payload?.CommitTimestamp);
        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Timestamp);
    }

    [Fact]
    public void WillDeserializeOldStyleVanityUrlEvent()
    {
        var messageBody = """
                          { "action": "nginx-vanity-urls",
                            "timestamp": "2024-10-23T15:10:10.123",
                            "content": {
                              "environment": "test",
                              "services": [
                                {"name": "service-a", "urls": [{"domain": "service.gov.uk", "host": "service-a"}]},
                                {"name": "service-b", "urls": [{"domain": "service.gov.uk", "host": "service-b"}]}
                              ]
                            }
                          }
                          """;

        var workflowEvent = JsonSerializer.Deserialize<Event<VanityUrlsPayload>>(messageBody);

        Assert.Equal("nginx-vanity-urls", workflowEvent?.Action);
        Assert.Equal("nginx-vanity-urls", workflowEvent?.EventType);

        Assert.Equal("test", workflowEvent?.Content?.Environment);
        Assert.Equal("test", workflowEvent?.Payload?.Environment);

        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Timestamp);


        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[0].Urls[0].Domain);
        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Urls[0].Host);

        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[1].Urls[0].Domain);
        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Urls[0].Host);
    }

    [Fact]
    public void WillDeserializeVanityUrlEvent()
    {
        var messageBody = """
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

        var workflowEvent = JsonSerializer.Deserialize<Event<VanityUrlsPayload>>(messageBody);

        Assert.Equal("nginx-vanity-urls", workflowEvent?.EventType);
        Assert.Equal("test", workflowEvent?.Payload?.Environment);

        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), workflowEvent?.Timestamp);

        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[0].Urls[0].Domain);
        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Urls[0].Host);

        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[1].Urls[0].Domain);
        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Urls[0].Host);
    }
}