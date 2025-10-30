using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.AutoTestRunTriggers;

public class AutoTestRunTriggerServiceTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{

    [Fact]
    public async Task AutoTestRunTriggerDeletesTriggerWithNoEnvironments()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService =
            new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var trigger = await autoTestRunTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);
        Assert.Null(trigger);

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
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
        Assert.Equal(["perf-test", "dev", "prod"], triggerFromDb.TestSuites["cdp-env-test-suite"][0].Environments);

        var updatedTrigger = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
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

    [Fact]
    public async Task RemoveTestRun_NonExistentTrigger_ReturnsNull()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var dto = new AutoTestRunTriggerDto
        {
            ServiceName = "non-existent-service",
            TestSuite = "some-test-suite",
            Profile = "default",
            Environments = []
        };

        var result = await autoTestRunTriggerService.RemoveTestRun(dto, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTestRun_NonExistentTrigger_ReturnsNull()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var dto = new AutoTestRunTriggerDto
        {
            ServiceName = "non-existent-service",
            TestSuite = "some-test-suite",
            Profile = "default",
            Environments = ["dev"]
        };

        var result = await autoTestRunTriggerService.UpdateTestRun(dto, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveTrigger_WithProfile_CreatesCorrectConfig()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "profile-test-service",
                "testSuite": "test-suite-with-profile",
                "profile": "custom-profile",
                "environments": ["dev", "test"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb = await autoTestRunTriggerService.FindForService("profile-test-service", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("profile-test-service", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal("custom-profile", triggerFromDb.TestSuites["test-suite-with-profile"][0].Profile);
        Assert.Equal(["dev", "test"], triggerFromDb.TestSuites["test-suite-with-profile"][0].Environments);
    }

    [Fact]
    public async Task SaveTrigger_MultipleConfigsForSameTestSuite_SavesCorrectly()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var firstConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "multi-config-service",
                "testSuite": "shared-test-suite",
                "profile": "profile1",
                "environments": ["dev"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(firstConfig, CancellationToken.None);

        var secondConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "multi-config-service",
                "testSuite": "shared-test-suite",
                "profile": "profile2",
                "environments": ["test"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(secondConfig, CancellationToken.None);

        var triggerFromDb = await autoTestRunTriggerService.FindForService("multi-config-service", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(2, triggerFromDb.TestSuites["shared-test-suite"].Count);
        Assert.Contains(triggerFromDb.TestSuites["shared-test-suite"], c => c.Profile == "profile1" && c.Environments.SequenceEqual(["dev"]));
        Assert.Contains(triggerFromDb.TestSuites["shared-test-suite"], c => c.Profile == "profile2" && c.Environments.SequenceEqual(["test"]));
    }

    [Fact]
    public async Task SaveTrigger_WithNullProfile_CreatesCorrectConfig()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var newTrigger = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "null-profile-service",
                "testSuite": "test-suite-null-profile",
                "profile": null,
                "environments": ["dev", "test"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(newTrigger, CancellationToken.None);
        var triggerFromDb = await autoTestRunTriggerService.FindForService("null-profile-service", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("null-profile-service", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Null(triggerFromDb.TestSuites["test-suite-null-profile"][0].Profile);
        Assert.Equal(["dev", "test"], triggerFromDb.TestSuites["test-suite-null-profile"][0].Environments);
    }

    [Fact]
    public async Task SaveTrigger_MixedNullAndNonNullProfiles_SavesCorrectly()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        var nullProfileConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "mixed-profile-service",
                "testSuite": "shared-test-suite",
                "profile": null,
                "environments": ["dev"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(nullProfileConfig, CancellationToken.None);

        var namedProfileConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "mixed-profile-service",
                "testSuite": "shared-test-suite",
                "profile": "custom",
                "environments": ["test"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(namedProfileConfig, CancellationToken.None);

        var triggerFromDb = await autoTestRunTriggerService.FindForService("mixed-profile-service", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Equal(2, triggerFromDb.TestSuites["shared-test-suite"].Count);
        Assert.Contains(triggerFromDb.TestSuites["shared-test-suite"], c => c.Profile == null && c.Environments.SequenceEqual(["dev"]));
        Assert.Contains(triggerFromDb.TestSuites["shared-test-suite"], c => c.Profile == "custom" && c.Environments.SequenceEqual(["test"]));
    }

    [Fact]
    public async Task RemoveTestRun_WithNullProfile_RemovesCorrectConfig()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var autoTestRunTriggerService = new AutoTestRunTriggerService(mongoFactory, new LoggerFactory());

        // First add two configs - one with null profile and one with named profile
        var nullProfileConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "remove-null-profile-service",
                "testSuite": "test-suite",
                "profile": null,
                "environments": ["dev"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(nullProfileConfig, CancellationToken.None);

        var namedProfileConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "remove-null-profile-service",
                "testSuite": "test-suite",
                "profile": "custom",
                "environments": ["test"]
            }
            """)!;

        await autoTestRunTriggerService.SaveTrigger(namedProfileConfig, CancellationToken.None);

        // Remove the null profile config
        var removeConfig = JsonSerializer.Deserialize<AutoTestRunTriggerDto>("""
            {
                "serviceName": "remove-null-profile-service",
                "testSuite": "test-suite",
                "profile": null,
                "environments": []
            }
            """)!;

        await autoTestRunTriggerService.RemoveTestRun(removeConfig, CancellationToken.None);
        var triggerFromDb = await autoTestRunTriggerService.FindForService("remove-null-profile-service", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Single(triggerFromDb.TestSuites);
        Assert.Single(triggerFromDb.TestSuites["test-suite"]);
        Assert.Equal("custom", triggerFromDb.TestSuites["test-suite"][0].Profile);
        Assert.Equal(["test"], triggerFromDb.TestSuites["test-suite"][0].Environments);
    }

}