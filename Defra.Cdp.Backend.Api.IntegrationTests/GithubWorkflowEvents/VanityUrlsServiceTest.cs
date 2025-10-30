using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class VanityUrlsServiceTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task VanityUrlsReturnsAsExpected()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var nginxVanityUrlsService = new NginxVanityUrlsService(mongoFactory, new LoggerFactory());

        var sampleEvent = EventFromJson<NginxVanityUrlsPayload>("""
                {
                  "eventType": "nginx-vanity-urls",
                  "timestamp": "2024-10-23T15:10:10.123",
                  "payload": {
                    "environment": "test",
                    "services": [
                      {"name": "service-a", "urls": [{"domain": "service.gov.uk", "host": "service-a"}]},
                      {"name": "service-b", "urls": [{"domain": "service.gov.uk", "host": "service-b"}]}
                    ]
                  }
                }
                """);

        await nginxVanityUrlsService.PersistEvent(sampleEvent, TestContext.Current.CancellationToken);

        var vanityUrlsService = new VanityUrlsService(mongoFactory);

        var result = await vanityUrlsService.FindService("service-a", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equal("service-a", result.First().ServiceName);
        Assert.Equal("service-a.service.gov.uk", result.First().Url);
        Assert.Equal("test", result.First().Environment);
        Assert.False(result.First().Shuttered);
        Assert.False(result.First().Enabled);

        var shutteredEvent = EventFromJson<ShutteredUrlsPayload>("""
                {
                    "eventType": "shuttered-urls", 
                    "timestamp": "2024-12-30T12:13:26.320415+00:00",
                     "payload": {
                         "environment": "test", 
                         "urls": [
                            "service-a.service.gov.uk"
                         ]
                     }
                 }
                """);

        var shutteredUrlsService = new ShutteredUrlsService(mongoFactory, new LoggerFactory());
        await shutteredUrlsService.PersistEvent(shutteredEvent, TestContext.Current.CancellationToken);

        var shutteredResult = await vanityUrlsService.FindService("service-a", TestContext.Current.CancellationToken);
        Assert.NotNull(shutteredResult);
        Assert.Single(shutteredResult);

        Assert.Equal("service-a", shutteredResult.First().ServiceName);
        Assert.Equal("service-a.service.gov.uk", shutteredResult.First().Url);
        Assert.Equal("test", shutteredResult.First().Environment);
        Assert.True(shutteredResult.First().Shuttered);
        Assert.False(shutteredResult.First().Enabled);

        var enabledEvent = EventFromJson<EnabledVanityUrlsPayload>("""
                {
                    "eventType": "enabled-urls", 
                    "timestamp": "2024-12-30T12:13:26.320415+00:00", 
                    "payload": {
                        "environment": "test", 
                        "urls": [ 
                            { "service": "service-a", "url": "service-a.service.gov.uk" } 
                        ]
                    }
                }
                """);

        var enabledVanityUrlsService = new EnabledVanityUrlsService(mongoFactory, new LoggerFactory());
        await enabledVanityUrlsService.PersistEvent(enabledEvent, TestContext.Current.CancellationToken);

        var enabledResult = await vanityUrlsService.FindService("service-a", TestContext.Current.CancellationToken);
        Assert.NotNull(enabledResult);
        Assert.Single(enabledResult);

        Assert.Equal("service-a", enabledResult.First().ServiceName);
        Assert.Equal("service-a.service.gov.uk", enabledResult.First().Url);
        Assert.Equal("test", enabledResult.First().Environment);
        Assert.True(enabledResult.First().Shuttered);
        Assert.True(enabledResult.First().Enabled);

        var unshutteredEvent = EventFromJson<ShutteredUrlsPayload>("""
                                                                 {
                                                                     "eventType": "shuttered-urls", 
                                                                     "timestamp": "2024-12-30T12:13:26.320415+00:00",
                                                                      "payload": {
                                                                          "environment": "test", 
                                                                          "urls": [
                                                                          ]
                                                                      }
                                                                  }
                                                                 """);

        await shutteredUrlsService.PersistEvent(unshutteredEvent, TestContext.Current.CancellationToken);

        var unshutteredResult = await vanityUrlsService.FindService("service-a", TestContext.Current.CancellationToken);
        Assert.NotNull(unshutteredResult);
        Assert.Single(unshutteredResult);

        Assert.Equal("service-a", unshutteredResult.First().ServiceName);
        Assert.Equal("service-a.service.gov.uk", unshutteredResult.First().Url);
        Assert.Equal("test", unshutteredResult.First().Environment);
        Assert.False(unshutteredResult.First().Shuttered);
        Assert.True(unshutteredResult.First().Enabled);
    }
}