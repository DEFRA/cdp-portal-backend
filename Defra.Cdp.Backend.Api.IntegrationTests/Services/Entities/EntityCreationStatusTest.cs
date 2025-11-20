using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public partial class EntityPlatformStateTests
{
    private readonly Entity _entityCompleted = new()
    {
        Name = "complete",
        Status = Status.Creating,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            { "dev", new CreationProgress { Complete = true } },
            { "test", new CreationProgress { Complete = true } },
            { "perf-test", new CreationProgress { Complete = true } },
            { "ext-test", new CreationProgress { Complete = true } },
            { "prod", new CreationProgress { Complete = true } },
            { "management", new CreationProgress { Complete = true } },
            { "infra-dev", new CreationProgress { Complete = true } }
        }
    };

    private readonly Entity _entityInProgress = new()
    {
        Name = "inprogress",
        Status = Status.Creating,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            { "dev", new CreationProgress { Complete = true } },
            { "test", new CreationProgress { Complete = true } },
            { "perf-test", new CreationProgress { Complete = true } },
            { "ext-test", new CreationProgress { Complete = true } },
            { "prod", new CreationProgress { Complete = false } },
            { "management", new CreationProgress { Complete = true } },
            { "infra-dev", new CreationProgress { Complete = true } }
        }
    };

    private readonly Entity _entityBeingDecommissioned = new()
    {
        Name = "decomming",
        Status = Status.Created,
        Type = Type.Microservice,
        Decommissioned =
            new Decommission
            {
                WorkflowsTriggered = true,
                DecommissionedBy = new UserDetails(),
                Started = new DateTime(),
                Finished = null
            },
        Progress = new Dictionary<string, CreationProgress>
        {
            { "dev", new CreationProgress { Complete = false } },
            { "test", new CreationProgress { Complete = false } },
            { "perf-test", new CreationProgress { Complete = true } },
            { "ext-test", new CreationProgress { Complete = true } },
            { "prod", new CreationProgress { Complete = false } },
            { "management", new CreationProgress { Complete = true } },
            { "infra-dev", new CreationProgress { Complete = true } }
        }
    };

    private readonly Entity _entityDecommissioned = new()
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

    private readonly Entity _entityWithRestrictedEnvs = new()
    {
        Name = "restricted",
        Status = Status.Created,
        Type = Type.Microservice,
        Progress = new Dictionary<string, CreationProgress>
        {
            { "management", new CreationProgress { Complete = true } },
            { "infra-dev", new CreationProgress { Complete = true } }
        },
        Metadata = new TenantMetadata { Environments = ["management"] }
    };

    [Fact]
    public async Task Updates_to_complete()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        await service.Create(_entityCompleted, TestContext.Current.CancellationToken);
        await service.Create(_entityInProgress, TestContext.Current.CancellationToken);
        await service.Create(_entityBeingDecommissioned, TestContext.Current.CancellationToken);
        await service.Create(_entityDecommissioned, TestContext.Current.CancellationToken);

        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        var complete = await service.GetEntity(_entityCompleted.Name, TestContext.Current.CancellationToken);
        var progress = await service.GetEntity(_entityInProgress.Name, TestContext.Current.CancellationToken);
        var decomming = await service.GetEntity(_entityBeingDecommissioned.Name, TestContext.Current.CancellationToken);
        var decommed = await service.GetEntity(_entityDecommissioned.Name, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Created, complete?.Status);
        Assert.Equal(Status.Creating, progress?.Status);
        Assert.Equal(Status.Decommissioning, decomming?.Status);
        Assert.Equal(Status.Decommissioned, decommed?.Status);

        // Check a second run doesn't change any state
        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        complete = await service.GetEntity(_entityCompleted.Name, TestContext.Current.CancellationToken);
        progress = await service.GetEntity(_entityInProgress.Name, TestContext.Current.CancellationToken);
        decomming = await service.GetEntity(_entityBeingDecommissioned.Name, TestContext.Current.CancellationToken);
        decommed = await service.GetEntity(_entityDecommissioned.Name, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Created, complete?.Status);
        Assert.Equal(Status.Creating, progress?.Status);
        Assert.Equal(Status.Decommissioning, decomming?.Status);
        Assert.Equal(Status.Decommissioned, decommed?.Status);
    }


    [Fact]
    public async Task Handles_environment_exceptions_correctly()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        await service.Create(_entityWithRestrictedEnvs, TestContext.Current.CancellationToken);
        await service.Create(_entityCompleted, TestContext.Current.CancellationToken);

        var platformPayload = new PlatformStatePayload
        {
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                {
                    _entityWithRestrictedEnvs.Name, new CdpTenantAndMetadata
                    {
                        Metadata = _entityWithRestrictedEnvs.Metadata,
                        Tenant = new CdpTenant(),
                        Progress =
                            new CreationProgress { Complete = true, Steps = new Dictionary<string, bool>() }
                    }
                },
                {
                    _entityCompleted.Name,
                    new CdpTenantAndMetadata
                    {
                        Tenant = new CdpTenant(),
                        Progress = new CreationProgress { Complete = true, Steps = new Dictionary<string, bool>() }
                    }
                }
            },
            TerraformSerials = new Serials(),
            Created = "",
            Version = 1
        };

        var userServiceTeams = new Dictionary<string, UserServiceTeam>();

        await service.UpdateEnvironmentState(platformPayload, userServiceTeams, TestContext.Current.CancellationToken);
        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        var restricted = await service.GetEntity(_entityWithRestrictedEnvs.Name, TestContext.Current.CancellationToken);
        Assert.Equal(Status.Created, restricted?.Status);

        // Restricted entity doesn't exist in prod, but should be ok.
        await service.UpdateEnvironmentState(
            new PlatformStatePayload
            {
                Environment = "prod",
                Tenants = new Dictionary<string, CdpTenantAndMetadata>
                {
                    {
                        _entityCompleted.Name, new CdpTenantAndMetadata
                        {
                            Tenant = new CdpTenant(),
                            Progress =
                                new CreationProgress { Complete = true, Steps = new Dictionary<string, bool>() }
                        }
                    }
                },
                TerraformSerials = new Serials(),
                Created = "",
                Version = 1
            }, userServiceTeams, TestContext.Current.CancellationToken);

        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        restricted = await service.GetEntity(_entityWithRestrictedEnvs.Name, TestContext.Current.CancellationToken);
        Assert.Equal(Status.Created, restricted?.Status);

        var completed = await service.GetEntity(_entityCompleted.Name, TestContext.Current.CancellationToken);
        Assert.Equal(Status.Created, completed?.Status);
    }

    [Fact]
    public async Task Handles_removal_of_services_normally()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var updatePayload = new PlatformStatePayload
        {
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                {
                    _entityCompleted.Name,
                    new CdpTenantAndMetadata
                    {
                        Metadata = _entityCompleted.Metadata,
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

        var userServiceTeams = new Dictionary<string, UserServiceTeam>();

        await service.Create(_entityCompleted, TestContext.Current.CancellationToken);
        await service.UpdateEnvironmentState(updatePayload, userServiceTeams, CancellationToken.None);
        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        var entity = await service.GetEntity(_entityCompleted.Name, TestContext.Current.CancellationToken);
        Assert.Equal(Status.Created, entity?.Status);

        // Update an environment in which the service doesn't exist.
        await service.UpdateEnvironmentState(
            new PlatformStatePayload
            {
                Environment = "management",
                Tenants = new Dictionary<string, CdpTenantAndMetadata>(),
                TerraformSerials = new Serials(),
                Created = "",
                Version = 1
            }, userServiceTeams, TestContext.Current.CancellationToken);

        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        entity = await service.GetEntity(_entityCompleted.Name, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("management", entity!.Progress.Keys);
        Assert.Contains("prod", entity.Progress.Keys);
    }
    
    [Fact]
    public async Task Cannot_revert_to_creating_from_created()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var completedEntityWithSomethingMissing = new Entity
        {
            Name = "completeWithSomethingMissing",
            Status = Status.Created,
            Type = Type.Microservice,
            Progress = new Dictionary<string, CreationProgress>
            {
                { "dev", new CreationProgress { Complete = false } },
                { "test", new CreationProgress { Complete = true } },
                { "perf-test", new CreationProgress { Complete = true } },
                { "ext-test", new CreationProgress { Complete = true } },
                { "prod", new CreationProgress { Complete = true } },
                { "management", new CreationProgress { Complete = true } },
                { "infra-dev", new CreationProgress { Complete = true } }
            }
        };
        
        await service.Create(completedEntityWithSomethingMissing, TestContext.Current.CancellationToken);
        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        var complete = await service.GetEntity(completedEntityWithSomethingMissing.Name, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Created, complete?.Status);
    }
    
    [Fact]
    public async Task Cannot_revert_to_decommissioning_from_decommissioned()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var completedEntityWithSomethingMissing = new Entity
        {
            Name = "decommissionedButItsStillGotSomething",
            Status = Status.Decommissioned,
            Type = Type.Microservice,
            Decommissioned = new Decommission
            {
                Started = DateTime.Now,
                Finished = DateTime.Now,
                DecommissionedBy = new UserDetails { DisplayName = "bob", Id = "123"},
                WorkflowsTriggered = true
            },
            Progress = new Dictionary<string, CreationProgress>
            {
                { "dev", new CreationProgress { Complete = false } }
            }
        };
        
        await service.Create(completedEntityWithSomethingMissing, TestContext.Current.CancellationToken);
        await service.BulkUpdateEntityStatus(TestContext.Current.CancellationToken);

        var entity = await service.GetEntity(completedEntityWithSomethingMissing.Name, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Decommissioned, entity?.Status);
    }

}