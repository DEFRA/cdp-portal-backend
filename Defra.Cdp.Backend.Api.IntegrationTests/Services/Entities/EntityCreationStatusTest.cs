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
}