using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class TenantRdsDatabasesServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
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
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantDatabases");
        var databaseService = new TenantRdsDatabasesService(mongoFactory, new NullLoggerFactory());

        var sampleEvent = TestData();

        await databaseService.PersistEvent(sampleEvent, CancellationToken.None);

        var resultByName = await databaseService.Find("fcp-mpdp-backend", null, CancellationToken.None);
        Assert.Single(resultByName);
        Assert.Equal("fcp_mpdp_backend", resultByName[0].DatabaseName);
        
        var resultByEnv = await databaseService.Find(null, "dev", CancellationToken.None);
        Assert.Equal(2, resultByEnv.Count);
        Assert.Equal("ai_model_test", resultByEnv[0].DatabaseName);
        Assert.Equal("fcp_mpdp_backend", resultByEnv[1].DatabaseName);
    }
    
    [Fact]
    public async Task WillCleanUpDeletedDatabases()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantDatabases");
        var databaseService = new TenantRdsDatabasesService(mongoFactory, new NullLoggerFactory());

        var sampleEvent = TestData();

        await databaseService.PersistEvent(sampleEvent, CancellationToken.None);
        var resultBeforeDelete = await databaseService.Find(null, "dev", CancellationToken.None);
        Assert.Equal(2, resultBeforeDelete.Count);
        
        sampleEvent.Payload.RdsDatabases.RemoveAt(1);
        await databaseService.PersistEvent(sampleEvent, CancellationToken.None);
        var resultAfterDelete = await databaseService.Find(null, "dev", CancellationToken.None);
        Assert.Single(resultAfterDelete);
    }
}