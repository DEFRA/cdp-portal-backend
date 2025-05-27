using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class AppConfigVersionsServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task AppConfigVersionsReturnsLatestEventByCommitTimestamp()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AppConfigVersions");
        var appConfigVersionsService = new AppConfigVersionsService(mongoFactory, new LoggerFactory());

        var sampleEvent = EventFromJson<AppConfigVersionPayload>("""
                {
                  "eventType": "app-config-version",
                  "timestamp": "2025-01-23T15:10:10.123",
                  "payload": {
                    "commitSha": "abc123",
                    "commitTimestamp": "2025-01-23T15:10:10.123",
                    "environment": "test"
                  }
                }
                """);

        await appConfigVersionsService.PersistEvent(sampleEvent, CancellationToken.None);

        var result = await appConfigVersionsService.FindLatestAppConfigVersion("test", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 1, 23, 15, 10, 10, 123), result.CommitTimestamp);

        var olderEvent = EventFromJson<AppConfigVersionPayload>("""
                                                                 {
                                                                   "eventType": "app-config-version",
                                                                   "timestamp": "2025-01-10T15:10:10.123",
                                                                   "payload": {
                                                                     "commitSha": "def456",
                                                                     "commitTimestamp": "2025-01-10T15:10:10.123",
                                                                     "environment": "test"
                                                                   }
                                                                 }
                                                                 """);

        await appConfigVersionsService.PersistEvent(olderEvent, CancellationToken.None);

        result = await appConfigVersionsService.FindLatestAppConfigVersion("test", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 1, 23, 15, 10, 10, 123), result.CommitTimestamp);

        var newerEvent = EventFromJson<AppConfigVersionPayload>("""
                                                                 {
                                                                   "eventType": "app-config-version",
                                                                   "timestamp": "2025-02-06T10:11:12.123",
                                                                   "payload": {
                                                                     "commitSha": "ghi789",
                                                                     "commitTimestamp": "2025-02-06T10:11:12.123",
                                                                     "environment": "test"
                                                                   }
                                                                 }
                                                                 """);

        await appConfigVersionsService.PersistEvent(newerEvent, CancellationToken.None);

        result = await appConfigVersionsService.FindLatestAppConfigVersion("test", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("ghi789", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 2, 6, 10, 11, 12, 123), result.CommitTimestamp);
    }
}