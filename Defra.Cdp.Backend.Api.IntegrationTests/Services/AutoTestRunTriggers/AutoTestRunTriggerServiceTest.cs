using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.AutoTestRunTriggers;

public class AutoTestRunTriggerServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task AutoTestRunTriggerOverwritesExistingTrigger()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());
        
        var trigger = await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);
        Assert.Null(trigger);
        
        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                {
                    "serviceName": "cdp-portal-frontend",
                    "environmentTestSuitesMap": {
                        "infra-dev": [
                          "cdp-env-test-suite"
                        ],
                        "development": [
                          "cdp-portal-perf-tests",
                          "cdp-env-test-suite"
                        ]
                    }
                            
                }
                """)!;
        
        await autoTestRunTriggerService.PersistTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.EnvironmentTestSuitesMap.Count);
        Assert.Equal(["infra-dev", "development"], triggerFromDb.EnvironmentTestSuitesMap.Keys);
        Assert.Equal(["cdp-env-test-suite"], triggerFromDb.EnvironmentTestSuitesMap["infra-dev"]);
        Assert.Equal(["cdp-portal-perf-tests", "cdp-env-test-suite"], triggerFromDb.EnvironmentTestSuitesMap["development"]);

        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                {
                    "serviceName": "cdp-portal-frontend",
                    "environmentTestSuitesMap": {
                        "infra-dev": [
                        ],
                        "development": [
                          "cdp-portal-perf-tests"
                        ],
                        "ext-test": [
                          "cdp-env-test-suite"
                        ]
                    }
                            
                }
                """)!;
        
        await autoTestRunTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.EnvironmentTestSuitesMap.Count);
        Assert.Equal(["development", "ext-test"], triggerFromDb.EnvironmentTestSuitesMap.Keys);
        Assert.Equal(["cdp-env-test-suite"], triggerFromDb.EnvironmentTestSuitesMap["ext-test"]);
        Assert.Equal(["cdp-portal-perf-tests"], triggerFromDb.EnvironmentTestSuitesMap["development"]);
    }
    
    [Fact]
    public async Task AutoTestRunTriggerDeletesTriggerWithNoEnvironments()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
        IAutoTestRunTriggerService autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());
        
        var trigger = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
        Assert.Null(trigger);
        
        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                {
                    "serviceName": "cdp-portal-backend",
                    "environmentTestSuitesMap": {
                        "perf-test": [
                          "cdp-portal-perf-tests",
                          "cdp-env-test-suite"
                        ],
                        "development": [
                          "cdp-env-test-suite"
                        ]
                    }
                            
                }
                """)!;
        
        await autoTestRunTriggerService.PersistTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.EnvironmentTestSuitesMap.Count);
        Assert.Equal(["perf-test", "development"], triggerFromDb.EnvironmentTestSuitesMap.Keys);
    
        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                {
                        "serviceName": "cdp-portal-backend",
                        "environmentTestSuitesMap": {}
                }
                """)!;
        
        await autoTestRunTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
        
        Assert.Null(triggerFromDb);
    }
    
    // [Fact]
    // public async Task AutoTestRunTriggerDoesntPersistProdEnvironment()
    // {
    //     var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
    //     IAutoTestRunTriggerService autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());
    //     
    //     var triggers = await autoTestRunTriggerService.FindAll(CancellationToken.None);
    //     Assert.Empty(triggers);
    //     
    //     var trigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
    //             {
    //                     "serviceName": "cdp-portal-backend",
    //                     "environments":
    //                         [
    //                            "prod",
    //                            "development"
    //                         ]
    //             }
    //             """)!;
    //     
    //     await autoTestRunTriggerService.PersistTrigger(trigger, CancellationToken.None);
    //     var triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
    //     
    //     Assert.NotNull(triggerFromDb);
    //     Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
    //     Assert.Single(triggerFromDb.Environments);
    //     Assert.Equal([ "development"], triggerFromDb.Environments);
    // }
}