using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.TestSuites;



public class TestRunServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)

{
    [Fact]
    public async Task ExistsTestRunReturnsFalseWhenTestRunDoesNotExist()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "testruns");
        var testRunService = new TestRunService(mongoFactory, new LoggerFactory());
        var exists = await testRunService.ExistsTestRunAsync("test-suite", "dev", "1234", new CancellationToken());
        Assert.False(exists);
    }
    
    [Fact]
    public async Task ExistsTestRunReturnsTrueWhenTestRunDoesExists()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "testruns");
        var testRunService = new TestRunService(mongoFactory, new LoggerFactory());
        
        var testRun = JsonSerializer.Deserialize<TestRun>("""
                                                          {
                                                            "_id": "68c97adb7e200c8c06ace0de",
                                                            "runId": "993948c3-8ba7-46ff-8907-4a6b6f01faf4",
                                                            "testSuite": "test-suite",
                                                            "environment": "dev",
                                                            "cpu": 4096,
                                                            "memory": 8192,
                                                            "user": {
                                                              "_id": "11111111-1111-1111-1111-111111111111",
                                                              "displayName": "Auto test runner"
                                                            },
                                                            "deployment": {
                                                              "deploymentId": "1234",
                                                              "version": "0.167.0",
                                                              "service": "my-service"
                                                            },
                                                            "created": "2025-09-16T14:57:31.383Z",
                                                            "taskArn": null,
                                                            "taskStatus": "starting",
                                                            "taskLastUpdate": null,
                                                            "testStatus": null,
                                                            "tag": "0.24.0",
                                                            "failureReasons": [],
                                                            "configVersion": "b81af8dfb378253cf4107eeb00cea44e2f579eab"
                                                          }
                                                          """)!;
        await testRunService.CreateTestRun(testRun, new CancellationToken());
        
        var exists = testRunService.ExistsTestRunAsync("test-suite", "dev", "1234", new CancellationToken()).GetAwaiter().GetResult();
        Assert.True(exists);
    }

}