using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.Extensions.Logging.Abstractions;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public class EntitiesServiceTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{

    [Fact]
    public async Task WillAddAndRemoveTags()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new NullLoggerFactory();
        var mongoFactory = CreateMongoDbClientFactory();
        var entitiesService = new EntitiesService(mongoFactory, logger);
        var entity = new Entity
        {
            Name = _fooRepository.Id,
            Teams = [],
            Status = Status.Created,
            Type = Type.Microservice
        };
        await entitiesService.Create(entity, ct);

        await entitiesService.AddTag(entity.Name, "tier 1", ct);
        await entitiesService.AddTag(entity.Name, "PRR", ct);
        var taggedEntity = await entitiesService.GetEntity(entity.Name, ct);

        Assert.Equivalent(taggedEntity?.Tags, new List<string> { "tier 1", "PRR" });

        await entitiesService.RemoveTag(entity.Name, "tier 1", ct);
        var untaggedEntity = await entitiesService.GetEntity(entity.Name, ct);
        Assert.Equivalent(untaggedEntity?.Tags, new List<string> { "PRR" });

    }
    
        
    [Fact]
    public async Task GetEntityIds_only_returns_entity_ids()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var entity = new Entity
        {
            Name = _fooRepository.Id,
            Teams = [],
            Status = Status.Created,
            Type = Type.Microservice
        };
        
        await service.Create(entity, TestContext.Current.CancellationToken);
        var result = await service.GetEntityIds(new EntityMatcher(), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([_fooRepository.Id], result);
    }

    
    [Fact]
    public async Task GetEntities_FiltersPartialNameAndTeamIdsTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new EntitiesService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var platformService = new Entity
        {
            Name = "tenant-platform-service",
            Teams = [new Team { TeamId = "platform", Name = "Platform" }],
            Status = Status.Created,
            Type = Type.Microservice
        };

        var otherTeamService = new Entity
        {
            Name = "tenant-other-service",
            Teams = [new Team { TeamId = "other-team", Name = "Other Team" }],
            Status = Status.Created,
            Type = Type.Microservice
        };

        await service.Create(platformService, ct);
        await service.Create(otherTeamService, ct);

        var matcher = new EntityMatcher(PartialName: "tenant", TeamIds: ["platform"]);
        var results = await service.GetEntities(matcher, ct);

        Assert.Single(results);
        Assert.Equal("tenant-platform-service", results[0].Name);
    }

    private readonly Repository _fooRepository = new()
    {
        Id = "foo",
        Teams = [new RepositoryTeam("foo-team", "1234", "foo-team")],
        IsArchived = false,
        IsTemplate = false,
        IsPrivate = false
    };
}