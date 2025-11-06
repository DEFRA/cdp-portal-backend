using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class TenantRdsDatabasesServiceTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{

    private CommonEvent<TenantDatabasePayload> TestData()
    {
        return EventFromJson<TenantDatabasePayload>("""
                                                    {
                                                      "eventType": "tenant-rds",
                                                      "timestamp": "2025-08-11T15:08:54.227001+00:00",
                                                      "payload": {
                                                        "environment": "dev",
                                                        "rds": [
                                                          {
                                                            "service": "ai-model-test",
                                                            "databaseName": "ai_model_test",
                                                            "endpoint": "ai-model-test.cluster-cfdfdfdf.eu-west-2.rds.amazonaws.com",
                                                            "readerEndpoint": "ai-model-test.cluster-ro-cfdfdfdf.eu-west-2.rds.amazonaws.com",
                                                            "engine": "aurora-postgresql",
                                                            "engineVersion": "16.6",
                                                            "port": 5432,
                                                            "earliestRestorableTime": "2025-07-12T07:06:18.191000+00:00",
                                                            "latestRestorableTime": "2025-08-11T15:06:08.796000+00:00",
                                                            "backupRetentionPeriod": 30
                                                          },
                                                          {
                                                            "service": "fcp-mpdp-backend",
                                                            "databaseName": "fcp_mpdp_backend",
                                                            "endpoint": "fcp-mpdp-backend.cluster-1111111111.eu-west-2.rds.amazonaws.com",
                                                            "readerEndpoint": "fcp-mpdp-backend.cluster-ro-11111111.eu-west-2.rds.amazonaws.com",
                                                            "engine": "aurora-postgresql",
                                                            "engineVersion": "16.6",
                                                            "port": 5432,
                                                            "earliestRestorableTime": "2025-07-12T07:08:06.646000+00:00",
                                                            "latestRestorableTime": "2025-08-11T15:05:00.353000+00:00",
                                                            "backupRetentionPeriod": 30
                                                          }
                                                        ]
                                                      }
                                                    }
                                                    """);
    }

    [Fact]
    public async Task WillUpdateDatabases()
    {

        var databaseService = new TenantRdsDatabasesService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var sampleEvent = TestData();

        await databaseService.PersistEvent(sampleEvent, TestContext.Current.CancellationToken);

        var results = await databaseService.FindAllForService("fcp-mpdp-backend", TestContext.Current.CancellationToken);
        Assert.Equal("fcp_mpdp_backend", results[0].DatabaseName);

        var result = await databaseService.FindForServiceByEnv("fcp-mpdp-backend", "dev", TestContext.Current.CancellationToken);
        Assert.Equal("fcp_mpdp_backend", result.DatabaseName);
    }

    [Fact]
    public async Task WillCleanUpDeletedDatabases()
    {
        var databaseService = new TenantRdsDatabasesService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var sampleEvent = TestData();

        await databaseService.PersistEvent(sampleEvent, TestContext.Current.CancellationToken);
        var resultBeforeDelete = await databaseService.FindAllForService("fcp-mpdp-backend", TestContext.Current.CancellationToken);
        Assert.Single(resultBeforeDelete);

        sampleEvent.Payload.RdsDatabases.RemoveAt(1);
        await databaseService.PersistEvent(sampleEvent, TestContext.Current.CancellationToken);
        var resultAfterDelete = await databaseService.FindAllForService("fcp-mpdp-backend", TestContext.Current.CancellationToken);
        Assert.Empty(resultAfterDelete);
    }
}