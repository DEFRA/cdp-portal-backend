using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.AutoDeploymentTriggers;

public class AutoDeploymentTriggerServiceTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task AutoDeploymentTriggerOverwritesExistingTrigger()
    {
        var mongoFactory = CreateConnectionFactory();
        var autoDeploymentTriggerService = new AutoDeploymentTriggerService(mongoFactory, new LoggerFactory());

        var triggers = await autoDeploymentTriggerService.FindAll(CancellationToken.None);
        Assert.Empty(triggers);

        var trigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-frontend",
                        "environments":
                            [
                               "infra-dev",
                               "development"
                            ]
                }
                """)!;

        await autoDeploymentTriggerService.PersistTrigger(trigger, CancellationToken.None);
        var triggerFromDb = await autoDeploymentTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal(["infra-dev", "development"], triggerFromDb.Environments);

        var updatedTrigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-frontend",
                        "environments":
                            [
                               "ext-test",
                               "test"
                            ]
                }
                """)!;

        await autoDeploymentTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoDeploymentTriggerService.FindForService("cdp-portal-frontend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal(["ext-test", "test"], triggerFromDb.Environments);
    }

    [Fact]
    public async Task AutoDeploymentTriggerDeletesTriggerWithNoEnvironments()
    {
        var mongoFactory = CreateConnectionFactory();
        IAutoDeploymentTriggerService autoDeploymentTriggerService = new AutoDeploymentTriggerService(mongoFactory, new LoggerFactory());

        var triggers = await autoDeploymentTriggerService.FindAll(CancellationToken.None);
        Assert.Empty(triggers);

        var trigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-backend",
                        "environments":
                            [
                               "infra-dev",
                               "development"
                            ]
                }
                """)!;

        await autoDeploymentTriggerService.PersistTrigger(trigger, CancellationToken.None);
        var triggerFromDb = await autoDeploymentTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal(["infra-dev", "development"], triggerFromDb.Environments);

        var updatedTrigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-backend",
                        "environments": [ ]
                }
                """)!;

        await autoDeploymentTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoDeploymentTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);

        Assert.Null(triggerFromDb);
    }

    [Fact]
    public async Task AutoDeploymentTriggerDoesntPersistProdEnvironment()
    {
        var mongoFactory = CreateConnectionFactory();
        IAutoDeploymentTriggerService autoDeploymentTriggerService = new AutoDeploymentTriggerService(mongoFactory, new LoggerFactory());

        var triggers = await autoDeploymentTriggerService.FindAll(CancellationToken.None);
        Assert.Empty(triggers);

        var trigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-backend",
                        "environments":
                            [
                               "prod",
                               "development"
                            ]
                }
                """)!;

        await autoDeploymentTriggerService.PersistTrigger(trigger, CancellationToken.None);
        var triggerFromDb = await autoDeploymentTriggerService.FindForService("cdp-portal-backend", CancellationToken.None);

        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Single(triggerFromDb.Environments);
        Assert.Equal(["development"], triggerFromDb.Environments);
    }
}