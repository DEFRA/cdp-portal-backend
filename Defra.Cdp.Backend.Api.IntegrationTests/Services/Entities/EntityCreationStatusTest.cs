using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public partial class EntityPlatformStateTests
{
    
    private Entity entityCompleted = new()
    {
        Name = "complete",
        Status = Status.Creating,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            {"dev", new CreationProgress { Complete = true }},
            {"test", new CreationProgress { Complete = true }},
            {"perf-test", new CreationProgress { Complete = true }},
            {"ext-test", new CreationProgress { Complete = true }},
            {"prod", new CreationProgress { Complete = true }},
            {"management", new CreationProgress { Complete = true }},
            {"infra-dev", new CreationProgress { Complete = true }},
        }
    };
    
    private Entity entityInProgress = new()
    {
        Name = "inprogress",
        Status = Status.Creating,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            {"dev", new CreationProgress { Complete = true }},
            {"test", new CreationProgress { Complete = true }},
            {"perf-test", new CreationProgress { Complete = true }},
            {"ext-test", new CreationProgress { Complete = true }},
            {"prod", new CreationProgress { Complete = false }},
            {"management", new CreationProgress { Complete = true }},
            {"infra-dev", new CreationProgress { Complete = true }},
        }
    };
    
    private Entity entityBeingDecommissioned = new()
    {
        Name = "decomming",
        Status = Status.Created,
        Type = Type.Microservice,
        Decommissioned = new Decommission
        {
            WorkflowsTriggered = true,
            DecommissionedBy = new UserDetails(),
            Started = new DateTime(),
            Finished = null
        },
        Progress = new Dictionary<string, CreationProgress>
        {
            {"dev", new CreationProgress { Complete = false }},
            {"test", new CreationProgress { Complete = false }},
            {"perf-test", new CreationProgress { Complete = true }},
            {"ext-test", new CreationProgress { Complete = true }},
            {"prod", new CreationProgress { Complete = false }},
            {"management", new CreationProgress { Complete = true }},
            {"infra-dev", new CreationProgress { Complete = true }},
        }
    };
    
    private Entity entityDecomissioned = new()
    {
        Name = "decommed",
        Status = Status.Decommissioning,
        Type = Type.Microservice,
        Decommissioned = new Decommission
        {
            WorkflowsTriggered = true,
            DecommissionedBy = new UserDetails(),
            Started = new DateTime(),
            Finished = new DateTime()
        },
        Progress = new Dictionary<string, CreationProgress>()
    };
    
    private Entity entityWithRestrictedEnvs = new()
    {
        Name = "restricted",
        Status = Status.Created,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            {"management", new CreationProgress { Complete = true }},
            {"infra-dev", new CreationProgress { Complete = true }},
        },
        Metadata = new TenantMetadata
        {
            Environments = ["management"]
        }
        
    };
    
    [Fact]
    public async Task Updates_to_complete()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        await service.Create(entityCompleted, CancellationToken.None);
        await service.Create(entityInProgress, CancellationToken.None);
        await service.Create(entityBeingDecommissioned, CancellationToken.None);
        await service.Create(entityDecomissioned, CancellationToken.None);
        
        await service.BulkUpdateCreationStatus(CancellationToken.None);

        var complete = await service.GetEntity(entityCompleted.Name, CancellationToken.None);
        var progress = await service.GetEntity(entityInProgress.Name, CancellationToken.None);
        var decomming = await service.GetEntity(entityBeingDecommissioned.Name, CancellationToken.None);
        var decommed = await service.GetEntity(entityDecomissioned.Name, CancellationToken.None);
        
        Assert.Equal(Status.Created, complete?.Status);
        Assert.Equal(Status.Creating, progress?.Status);
        Assert.Equal(Status.Decommissioning, decomming?.Status);
        Assert.Equal(Status.Decommissioned, decommed?.Status);
        
        // Check a second run doesn't change any state
        await service.BulkUpdateCreationStatus(CancellationToken.None);

        complete = await service.GetEntity(entityCompleted.Name, CancellationToken.None);
        progress = await service.GetEntity(entityInProgress.Name, CancellationToken.None);
        decomming = await service.GetEntity(entityBeingDecommissioned.Name, CancellationToken.None);
        decommed = await service.GetEntity(entityDecomissioned.Name, CancellationToken.None);
        
        Assert.Equal(Status.Created, complete?.Status);
        Assert.Equal(Status.Creating, progress?.Status);
        Assert.Equal(Status.Decommissioning, decomming?.Status);
        Assert.Equal(Status.Decommissioned, decommed?.Status);
    }
    
    
    [Fact]
    public async Task Handles_environment_exceptions_correctly()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        await service.Create(entityWithRestrictedEnvs, CancellationToken.None);
        await service.Create(entityCompleted, CancellationToken.None);
        
        var platformPayload = new PlatformStatePayload
        {
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                {
                    entityWithRestrictedEnvs.Name,
                    new CdpTenantAndMetadata
                    {
                        Metadata = entityWithRestrictedEnvs.Metadata,
                        Tenant = new CdpTenant(),
                        Progress =
                            new CreationProgress { Complete = true, Steps = new Dictionary<string, bool>() }
                    }
                },
                {
                    entityCompleted.Name,
                    new CdpTenantAndMetadata
                    {
                        Tenant = new CdpTenant(),
                        Progress = new CreationProgress
                        {
                            Complete = true, Steps = new Dictionary<string, bool>()
                        }
                    }
                }
            },
            TerraformSerials = new Serials(),
            Created = "",
            Version = 1
        };
        await service.UpdateEnvironmentState(platformPayload, CancellationToken.None);
        await service.BulkUpdateCreationStatus(CancellationToken.None);


        var restricted = await service.GetEntity(entityWithRestrictedEnvs.Name, CancellationToken.None);
        Assert.Equal(Status.Created, restricted?.Status);
        
        // Restricted entity doesn't exist in prod, but should be ok.
        await service.UpdateEnvironmentState(new PlatformStatePayload
        {
            Environment = "prod",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                {
                    entityCompleted.Name, new CdpTenantAndMetadata {
                        Tenant = new CdpTenant(),
                        Progress = new CreationProgress { Complete = true, Steps = new Dictionary<string, bool>()}
                    }
                }
            },
            TerraformSerials = new Serials(),
            Created = "",
            Version = 1
        }, CancellationToken.None);
        
        await service.BulkUpdateCreationStatus(CancellationToken.None);

        restricted = await service.GetEntity(entityWithRestrictedEnvs.Name, CancellationToken.None);
        Assert.Equal(Status.Created, restricted?.Status);

        var completed = await service.GetEntity(entityCompleted.Name, CancellationToken.None);
        Assert.Equal(Status.Created, completed?.Status );
    }
    
    [Fact]
    public async Task Handles_removal_of_services_normally()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var updatePayload = new PlatformStatePayload
        {
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                {
                    entityCompleted.Name,
                    new CdpTenantAndMetadata
                    {
                        Metadata = entityCompleted.Metadata,
                        Tenant = new CdpTenant(),
                        Progress = new CreationProgress
                        {
                            Complete = true, Steps = new Dictionary<string, bool>()
                        }
                    }
                }
            },
            TerraformSerials = new Serials(),
            Created = "",
            Version = 1
        };
        
        await service.Create(entityCompleted, CancellationToken.None);
        await service.UpdateEnvironmentState(updatePayload, CancellationToken.None);
        await service.BulkUpdateCreationStatus(CancellationToken.None);

        var entity = await service.GetEntity(entityCompleted.Name, CancellationToken.None);
        Assert.Equal(Status.Created, entity?.Status);
        
        // Update an environment in which the service doesn't exist.
        await service.UpdateEnvironmentState(new PlatformStatePayload
        {
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>(),
            TerraformSerials = new Serials(),
            Created = "",
            Version = 1
        }, CancellationToken.None);
        
        await service.BulkUpdateCreationStatus(CancellationToken.None);

        entity = await service.GetEntity(entityCompleted.Name, CancellationToken.None);
        Assert.DoesNotContain("management", entity!.Progress.Keys);
        Assert.Contains("prod", entity.Progress.Keys);
    }
}