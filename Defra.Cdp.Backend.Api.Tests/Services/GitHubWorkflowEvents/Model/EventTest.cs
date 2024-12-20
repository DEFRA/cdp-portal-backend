using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.GitHubWorkflowEvents.Model;

public class EventTest
{
    [Fact]
    public void WillDeserializeAppConfigVersionEvent()
    {
        var messageBody = """
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

        var workflowEvent = JsonSerializer.Deserialize<Event<AppConfigVersionPayload>>(messageBody);

        Assert.Equal("app-config-version", workflowEvent?.EventType);
        Assert.Equal("infra-dev", workflowEvent?.Payload?.Environment);
        Assert.Equal("abc123", workflowEvent?.Payload?.CommitSha);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123), workflowEvent?.Payload?.CommitTimestamp);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);
    }
    
    [Fact]
    public void WillDeserializeVanityUrlEvent()
    {
        var messageBody = """
                          { "eventType": "nginx-vanity-urls",
                            "timestamp": "2024-11-23T15:10:10.123123+00:00",
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

        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123,123), workflowEvent?.Timestamp);

        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[0].Urls[0].Domain);
        Assert.Equal("service-a", workflowEvent?.Payload?.Services[0].Urls[0].Host);

        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload?.Services[1].Urls[0].Domain);
        Assert.Equal("service-b", workflowEvent?.Payload?.Services[1].Urls[0].Host);
    }

    [Fact]
    public void WillDeserializSquidProxyConfigEvent()
    {
        var messageBody = """
                          { "eventType": "squid-proxy-config",
                            "timestamp": "2024-11-23T15:10:10.123123+00:00",
                            "payload": {
                              "environment": "test",
                              "default_domains": [".cdp-int.defra.cloud", ".amazonaws.com", "login.microsoftonline.com", "www.gov.uk"],
                              "services": [
                                  {"name": "cdp-dotnet-tracing", "allowed_domains": []},
                                  {"name": "phi-frontend", "allowed_domains": ["gd.eppo.int"]}
                              ]
                            }
                          }
                          """;

        var workflowEvent = JsonSerializer.Deserialize<Event<SquidProxyConfigPayload>>(messageBody);

        Assert.Equal("squid-proxy-config", workflowEvent?.EventType);
        Assert.Equal("test", workflowEvent?.Payload?.Environment);
        
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent.Timestamp);

        Assert.Equal(4, workflowEvent?.Payload?.DefaultDomains.Count);
        Assert.Equal(".cdp-int.defra.cloud", workflowEvent?.Payload?.DefaultDomains[0]);
        Assert.Equal(".amazonaws.com", workflowEvent?.Payload?.DefaultDomains[1]);
        Assert.Equal("login.microsoftonline.com", workflowEvent?.Payload?.DefaultDomains[2]);
        Assert.Equal("www.gov.uk", workflowEvent?.Payload?.DefaultDomains[3]);
        
        Assert.Equal(2, workflowEvent?.Payload?.Services.Count);
        Assert.Equal("cdp-dotnet-tracing", workflowEvent?.Payload?.Services[0].Name);
        Assert.Equal(0, workflowEvent?.Payload?.Services[0].AllowedDomains.Count);
        
        Assert.Equal("phi-frontend", workflowEvent?.Payload?.Services[1].Name);
        Assert.Equal(1, workflowEvent?.Payload?.Services[1].AllowedDomains.Count);
        Assert.Equal("gd.eppo.int", workflowEvent?.Payload?.Services[1].AllowedDomains[0]);
    }
}