using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.AutoDeploymentTriggers;
using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Aws.AutoDeploymentTriggers;

public class AutoDeploymentTriggerServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task AutoDeploymentTriggerOverwritesExistingTrigger()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoDeploymentTriggers");
        var autoDeploymentTriggerService = new AutoDeploymentTriggerService(mongoFactory, new LoggerFactory());
        
        var noTrigger = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-frontend", CancellationToken.None);
        Assert.Null(noTrigger);
        
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
        var triggerFromDb = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-frontend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal([ "infra-dev", "development"], triggerFromDb.Environments);

        var updatedTrigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-frontend",
                        "environments":
                            [
                               "ext-test",
                               "prod"
                            ]
                }
                """)!;
        
        await autoDeploymentTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-frontend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-frontend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal(["ext-test", "prod"], triggerFromDb.Environments);
    }
    
    [Fact]
    public async Task AutoDeploymentTriggerDeletesTriggerWithNoEnvironments()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "AutoDeploymentTriggers");
        IAutoDeploymentTriggerService autoDeploymentTriggerService = new AutoDeploymentTriggerService(mongoFactory, new LoggerFactory());
        
        var noTrigger = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-backend", CancellationToken.None);
        Assert.Null(noTrigger);
        
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
        var triggerFromDb = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-backend", CancellationToken.None);
        
        Assert.NotNull(triggerFromDb);
        Assert.Equal("cdp-portal-backend", triggerFromDb.ServiceName);
        Assert.Equal(2, triggerFromDb.Environments.Count);
        Assert.Equal([ "infra-dev", "development"], triggerFromDb.Environments);

        var updatedTrigger = JsonSerializer.Deserialize<AutoDeploymentTrigger>("""
                {
                        "serviceName": "cdp-portal-backend",
                        "environments": [ ]
                }
                """)!;
        
        await autoDeploymentTriggerService.PersistTrigger(updatedTrigger, CancellationToken.None);
        triggerFromDb = await autoDeploymentTriggerService.FindForServiceName("cdp-portal-backend", CancellationToken.None);
        
        Assert.Null(triggerFromDb);
    }
}