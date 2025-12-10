using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class AppConfigVersionsServiceTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task AppConfigVersionsReturnsLatestEventByCommitTimestamp()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var appConfigVersionsService = new AppConfigVersionsService(connectionFactory, new LoggerFactory());

        var sampleEvent = """
                {
                  "eventType": "app-config-version",
                  "timestamp": "2025-01-23T15:10:10.123",
                  "payload": {
                    "commitSha": "abc123",
                    "commitTimestamp": "2025-01-23T15:10:10.123",
                    "environment": "test"
                  }
                }
                """;

        await appConfigVersionsService.Handle(sampleEvent, TestContext.Current.CancellationToken);

        var result = await appConfigVersionsService.FindLatestAppConfigVersion("test", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 1, 23, 15, 10, 10, 123), result.CommitTimestamp);

        var olderEvent = """
                         {
                            "eventType": "app-config-version",
                            "timestamp": "2025-01-10T15:10:10.123",
                            "payload": {
                              "commitSha": "def456",
                              "commitTimestamp": "2025-01-10T15:10:10.123",
                              "environment": "test"
                           }
                         }
                         """;

        await appConfigVersionsService.Handle(olderEvent, TestContext.Current.CancellationToken);

        result = await appConfigVersionsService.FindLatestAppConfigVersion("test", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 1, 23, 15, 10, 10, 123), result.CommitTimestamp);

        var newerEvent ="""
                         {
                           "eventType": "app-config-version",
                           "timestamp": "2025-02-06T10:11:12.123",
                           "payload": {
                             "commitSha": "ghi789",
                             "commitTimestamp": "2025-02-06T10:11:12.123",
                             "environment": "test"
                           }
                         }
                         """;

        await appConfigVersionsService.Handle(newerEvent, TestContext.Current.CancellationToken);

        result = await appConfigVersionsService.FindLatestAppConfigVersion("test", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("ghi789", result.CommitSha);
        Assert.Equal("test", result.Environment);
        Assert.Equal(new DateTime(2025, 2, 6, 10, 11, 12, 123), result.CommitTimestamp);
    }
}