using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.GitHubWorkflowEvents.Model;

public class EventTest
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

        var workflowEvent = JsonSerializer.Deserialize<Event<AppConfigVersionPayload>>(messageBody);

        Assert.Equal("app-config-version", workflowEvent?.EventType);
        Assert.Equal("infra-dev", workflowEvent?.Payload.Environment);
        Assert.Equal("abc123", workflowEvent?.Payload.CommitSha);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123), workflowEvent?.Payload.CommitTimestamp);
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);
    }
    
    [Fact]
    public void WillDeserializeVanityUrlEvent()
    {
        const string messageBody = """
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

        var workflowEvent = JsonSerializer.Deserialize<Event<NginxVanityUrlsPayload>>(messageBody);

        Assert.Equal("nginx-vanity-urls", workflowEvent?.EventType);
        Assert.Equal("test", workflowEvent?.Payload.Environment);

        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123,123), workflowEvent?.Timestamp);

        Assert.Equal("service-a", workflowEvent?.Payload.Services[0].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload.Services[0].Urls[0].Domain);
        Assert.Equal("service-a", workflowEvent?.Payload.Services[0].Urls[0].Host);

        Assert.Equal("service-b", workflowEvent?.Payload.Services[1].Name);
        Assert.Equal("service.gov.uk", workflowEvent?.Payload.Services[1].Urls[0].Domain);
        Assert.Equal("service-b", workflowEvent?.Payload.Services[1].Urls[0].Host);
    }

    [Fact]
    public void WillDeserializeSquidProxyConfigEvent()
    {
        const string messageBody = """
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
        Assert.Equal("test", workflowEvent?.Payload.Environment);
        
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);

        Assert.Equal(4, workflowEvent?.Payload.DefaultDomains.Count);
        Assert.Equal(".cdp-int.defra.cloud", workflowEvent?.Payload.DefaultDomains[0]);
        Assert.Equal(".amazonaws.com", workflowEvent?.Payload.DefaultDomains[1]);
        Assert.Equal("login.microsoftonline.com", workflowEvent?.Payload.DefaultDomains[2]);
        Assert.Equal("www.gov.uk", workflowEvent?.Payload.DefaultDomains[3]);
        
        Assert.Equal(2, workflowEvent?.Payload.Services.Count);
        Assert.Equal("cdp-dotnet-tracing", workflowEvent?.Payload.Services[0].Name);
        Assert.Equal(0, workflowEvent?.Payload.Services[0].AllowedDomains.Count);
        
        Assert.Equal("phi-frontend", workflowEvent?.Payload.Services[1].Name);
        Assert.Equal(1, workflowEvent?.Payload.Services[1].AllowedDomains.Count);
        Assert.Equal("gd.eppo.int", workflowEvent?.Payload.Services[1].AllowedDomains[0]);
    }

    [Fact]
    public void WillDeserializeTenantBucketEvent()
    {
        const string messageBody = """
                                   {
                                     "eventType": "tenant-buckets",
                                     "timestamp": "2024-11-23T15:10:10.123123+00:00",
                                     "payload": {
                                       "environment": "test",
                                       "buckets": [
                                         {
                                           "name": "frontend-service-bucket",
                                           "exists": true,
                                           "services_with_access": [
                                             "frontend-service",
                                             "backend-service"
                                           ]
                                         },
                                         {
                                           "name": "backend-service-bucket",
                                           "exists": false,
                                           "services_with_access": [
                                             "backend-service"
                                           ]
                                         }
                                       ]
                                     }
                                   }

                                   """;
        
        var workflowEvent = JsonSerializer.Deserialize<Event<TenantBucketsPayload>>(messageBody);

        Assert.Equal("tenant-buckets", workflowEvent?.EventType);
        Assert.Equal("test", workflowEvent?.Payload.Environment);
        
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);

        Assert.Equal("frontend-service-bucket", workflowEvent?.Payload.Buckets[0].Name);
        Assert.True(workflowEvent?.Payload.Buckets[0].Exists);
        Assert.Equal("frontend-service", workflowEvent?.Payload.Buckets[0].ServicesWithAccess[0]);
        Assert.Equal("backend-service", workflowEvent?.Payload.Buckets[0].ServicesWithAccess[1]);
        
        Assert.Equal("backend-service-bucket", workflowEvent?.Payload.Buckets[1].Name);
        Assert.False(workflowEvent?.Payload.Buckets[1].Exists);
        Assert.Equal("backend-service", workflowEvent?.Payload.Buckets[1].ServicesWithAccess[0]);

    }
    
        [Fact]
    public void WillDeserializeTenantServicesEvent()
    {
        const string messageBody = """
                                   {
                                     "eventType": "tenant-services",
                                     "timestamp": "2024-11-23T15:10:10.123123+00:00",
                                     "payload": {
                                       "environment": "test",
                                       "services": [
                                         {
                                           "zone": "public",
                                           "mongo": false,
                                           "redis": true,
                                           "service_code": "CDP",
                                           "test_suite": "frontend-service-tests",
                                           "name": "frontend-service",
                                           "buckets": [
                                             "frontend-service-buckets-*"
                                           ],
                                           "queues": [
                                             "frontend-service-queue"
                                           ]
                                         },
                                         {
                                           "zone": "protected",
                                           "mongo": true,
                                           "redis": false,
                                           "service_code": "CDP",
                                           "name": "backend-service"
                                         }
                                       ]
                                     }
                                   }
                                   """;
        
        var workflowEvent = JsonSerializer.Deserialize<Event<TenantServicesPayload>>(messageBody);

        Assert.Equal("tenant-services", workflowEvent?.EventType);
        Assert.Equal("test", workflowEvent?.Payload.Environment);
        
        Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);

        Assert.Equal("frontend-service", workflowEvent?.Payload.Services[0].Name);
        Assert.Equal("public", workflowEvent?.Payload.Services[0].Zone);
        Assert.False(workflowEvent?.Payload.Services[0].Mongo);
        Assert.True(workflowEvent?.Payload.Services[0].Redis);
        Assert.Equal("CDP", workflowEvent?.Payload.Services[0].ServiceCode);
        Assert.Equal("frontend-service-tests", workflowEvent?.Payload.Services[0].TestSuite);
        Assert.Equal("frontend-service-buckets-*", workflowEvent?.Payload.Services[0].Buckets?[0]);
        Assert.Equal("frontend-service-queue", workflowEvent?.Payload.Services[0].Queues?[0]);
        
        Assert.Equal("backend-service", workflowEvent?.Payload.Services[1].Name);
        Assert.Equal("protected", workflowEvent?.Payload.Services[1].Zone);
        Assert.True(workflowEvent?.Payload.Services[1].Mongo);
        Assert.False(workflowEvent?.Payload.Services[1].Redis);
        Assert.Equal("CDP", workflowEvent?.Payload.Services[1].ServiceCode);
    }
}