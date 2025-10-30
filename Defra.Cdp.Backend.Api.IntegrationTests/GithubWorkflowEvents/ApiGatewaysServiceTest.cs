using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class ApiGatewaysServiceTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task ApiGatewaysReturnsAsExpected()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var enabledApisService = new EnabledApisService(connectionFactory, new LoggerFactory());

        var sampleEvent = EventFromJson<EnabledApisPayload>("""
                {
                    "eventType": "enabled-apis",
                    "timestamp": "2025-02-02T10:59:02.445635+00:00",
                    "payload": {
                        "environment": "ext-test",
                        "apis":
                            [
                               {
                                   "service": "pha-import-notifications",
                                   "api": "pha-import-notifications.integration.api.defra.gov.uk"
                               }
                            ]
                    }
                }
                """);

        await enabledApisService.PersistEvent(sampleEvent, TestContext.Current.CancellationToken);

        var apiGatewaysService = new ApiGatewaysService(connectionFactory);

        var result = await apiGatewaysService.FindService("pha-import-notifications", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.Equal("pha-import-notifications", result.First().Service);
        Assert.Equal("pha-import-notifications.integration.api.defra.gov.uk", result.First().Api);
        Assert.Equal("ext-test", result.First().Environment);
        Assert.False(result.First().Shuttered);

        var shutteredEvent = EventFromJson<ShutteredUrlsPayload>("""
                {
                    "eventType": "shuttered-urls", 
                    "timestamp": "2024-12-30T12:13:26.320415+00:00",
                     "payload": {
                         "environment": "ext-test", 
                         "urls": [
                            "pha-import-notifications.integration.api.defra.gov.uk"
                         ]
                     }
                 }
                """);

        var shutteredUrlsService = new ShutteredUrlsService(connectionFactory, new LoggerFactory());
        await shutteredUrlsService.PersistEvent(shutteredEvent, TestContext.Current.CancellationToken);

        var shutteredResult = await apiGatewaysService.FindService("pha-import-notifications", TestContext.Current.CancellationToken);
        Assert.NotNull(shutteredResult);
        Assert.Single(shutteredResult);

        Assert.Equal("pha-import-notifications", shutteredResult.First().Service);
        Assert.Equal("pha-import-notifications.integration.api.defra.gov.uk", shutteredResult.First().Api);
        Assert.Equal("ext-test", shutteredResult.First().Environment);
        Assert.True(shutteredResult.First().Shuttered);
    }
}