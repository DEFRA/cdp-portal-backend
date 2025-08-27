using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public class EntitiesServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{

    [Fact]
    public async Task WillAddAndRemoveTags()
    {
        var ct = CancellationToken.None;
        var logger = new NullLoggerFactory();
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var entitiesService = new EntitiesService(mongoFactory, logger);
        var entity = new Entity
        {
            Name = _fooRepository.Id,
            Teams = [],
            Status = Status.Created,
            Type = Type.Microservice,
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
    public async Task WillRefreshTeams()
    {
        var logger = new NullLoggerFactory();
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, logger);
        var entitiesService = new EntitiesService(mongoFactory, logger);

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);

        var entityToCreate = new Entity
        {
            Name = _fooRepository.Id,
            Teams = [],
            Status = Status.Created,
            Type = Type.Microservice,
        };

        await entitiesService.Create(entityToCreate, CancellationToken.None);

        // Check initial state
        var entity = await entitiesService.GetEntity(_fooRepository.Id, CancellationToken.None);
        Assert.NotNull(entity);
        Assert.Empty(entity.Teams);


        // Update the teams in github
        List<Repository> repos =
        [
            new Repository
            {
                Id = "foo",
                Teams = [new RepositoryTeam("bar-team", "9999", "bar-team")],
                IsArchived = false,
                IsTemplate = false,
                IsPrivate = false
            }
        ];

        // Force refresh
        await entitiesService.RefreshTeams(repos, CancellationToken.None);

        var updatedEntity = await entitiesService.GetEntity(_fooRepository.Id, CancellationToken.None);

        Assert.NotNull(updatedEntity);
        var team = new Team { TeamId = "9999", Name = "bar-team" };
        Assert.Contains(team, updatedEntity.Teams);
        Assert.Single(updatedEntity.Teams);
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