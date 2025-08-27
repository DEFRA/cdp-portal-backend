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
    public async Task AutoTestRunTriggerAddingTestRunsTrigger()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var trigger = await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);
        Assert.Null(trigger);

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                        {
                                                                            "serviceName": "cdp-portal-frontend",
                                                                            "testSuite": "cdp-env-test-suite",
                                                                            "environments": ["infra-dev", "dev"]
                                                                        }
                                                                        """)!;

        await autoTestRunTriggerService.SaveTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb =
            await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(new List<string> { "cdp-env-test-suite" }, triggerFromDb.TestSuites.Keys);
        Assert.Equal(["infra-dev", "dev"], triggerFromDb.TestSuites["cdp-env-test-suite"]);

        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                            {
                                                                                "serviceName": "cdp-portal-frontend",
                                                                                "testSuite": "cdp-portal-perf-tests",
                                                                                "environments": ["dev"]
                                                                            }
                                                                            """)!;

        await autoTestRunTriggerService.SaveTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.TestSuites.Count);
        Assert.Equal(new List<string> { "cdp-env-test-suite", "cdp-portal-perf-tests" }, triggerFromDb.TestSuites.Keys);
        Assert.Equal(["infra-dev", "dev"], triggerFromDb.TestSuites["cdp-env-test-suite"]);
        Assert.Equal(["dev"], triggerFromDb.TestSuites["cdp-portal-perf-tests"]);
    }

    [Fact]
    public async Task AutoTestRunTriggerUpdatingTestRunsTrigger()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var trigger = await autoTestRunTriggerService.FindForService("cdp-user-service-backend", CancellationToken
            .None);
        Assert.Null(trigger);

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                        {
                                                                            "serviceName": "cdp-user-service-backend",
                                                                            "testSuite": "cdp-portal-journey-tests",
                                                                            "environments": ["infra-dev", "dev", "test"]
                                                                        }
                                                                        """)!;

        await autoTestRunTriggerService.SaveTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb =
            await autoTestRunTriggerService.FindForService("cdp-user-service-backend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-user-service-backend", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(new List<string> { "cdp-portal-journey-tests" }, triggerFromDb.TestSuites.Keys);
        Assert.Equal(["infra-dev", "dev", "test"], triggerFromDb.TestSuites["cdp-portal-journey-tests"]);

        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                            {
                                                                                "serviceName": "cdp-user-service-backend",
                                                                                "testSuite": "cdp-portal-journey-tests",
                                                                                "environments": ["dev"]
                                                                            }
                                                                            """)!;

        await autoTestRunTriggerService.SaveTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-user-service-backend", CancellationToken
            .None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-user-service-backend", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(new List<string> { "cdp-portal-journey-tests" }, triggerFromDb.TestSuites.Keys);
        Assert.Equal(["dev"], triggerFromDb.TestSuites["cdp-portal-journey-tests"]);
    }

    [Fact]
    public async Task AutoTestRunTriggerDeletesTriggerWithNoEnvironments()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoTestRunTriggers");
        var autoTestRunTriggerService =
            new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var trigger = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
        Assert.Null(trigger);

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                        {
                                                                            "serviceName": "cdp-portal-backend",
                                                                            "testSuite": "cdp-env-test-suite",
                                                                            "environments": ["perf-test", "dev", "prod"]
                                                                        }
                                                                        """)!;

        await autoTestRunTriggerService.SaveTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb =
            await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(new List<string> { "cdp-env-test-suite" }, triggerFromDb.TestSuites.Keys);
        Assert.Equal(["perf-test", "dev", "prod"], triggerFromDb.TestSuites["cdp-env-test-suite"]);

        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTrigger>("""
                                                                            {
                                                                                    "serviceName": "cdp-portal-backend",
                                                                                    "testSuite": "cdp-env-test-suite",
                                                                                    "environments": []
                                                                            }
                                                                            """)!;

        await autoTestRunTriggerService.SaveTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Empty(triggerFromDb.TestSuites);
    }
}