using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Shuttering;

public class ShutteringTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{

    [Fact]
    public async Task TestShutteringStatus()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var entityService = new EntitiesService(connectionFactory, new NullLoggerFactory());
        var shutteringArchiveService = new ShutteringArchiveService(connectionFactory, new NullLoggerFactory());
        var shutteringService = new ShutteringService(connectionFactory, entityService, shutteringArchiveService, new NullLoggerFactory());
        var ct = TestContext.Current.CancellationToken;

        await entityService.Create(_entity, ct);
        await shutteringService.Register(new ShutteringRecord("prod", _entity.Name, "foo.com", "", true, new UserDetails(),
            DateTime.Now), ct);

        var states = await shutteringService.ShutteringStatesForService(_entity.Name, ct);
        Assert.Single(states);
        Assert.Equal(ShutteringStatus.PendingShuttered, states[0].Status);

        var stateByUrl = await shutteringService.ShutteringStatesForService(_entity.Name, "foo.com", ct);
        Assert.NotNull(stateByUrl);
        Assert.Equal(ShutteringStatus.PendingShuttered, stateByUrl.Status);

        var missingState = await shutteringService.ShutteringStatesForService(_entity.Name, "bar.com", ct);
        Assert.Null(missingState);
    }

    [Fact]
    public async Task TestShutteringStatusIsDrivenFromEntity()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var entitiesCollection = connectionFactory.GetCollection<Entity>("entities");
        var entityService = new EntitiesService(connectionFactory, new NullLoggerFactory());
        var shutteringArchiveService = new ShutteringArchiveService(connectionFactory, new NullLoggerFactory());
        var shutteringService = new ShutteringService(connectionFactory, entityService, shutteringArchiveService, new NullLoggerFactory());
        var ct = TestContext.Current.CancellationToken;

        await entityService.Create(_entity, ct);
        await shutteringService.Register(new ShutteringRecord("prod", _entity.Name, "foo.com", "", true, new UserDetails(),
            DateTime.Now), ct);


        // requested true, actual false
        var status = await shutteringService.ShutteringStatesForService(_entity.Name, "foo.com", ct);
        Assert.NotNull(status);
        Assert.Equal(ShutteringStatus.PendingShuttered, status.Status);

        // Update entity so that shuttering is complete
        var updatedEntity = await entityService.GetEntity(_entity.Name, ct);
        Assert.NotNull(updatedEntity);

        updatedEntity.Environments["prod"].Urls["foo.com"].Shuttered = true;
        await entitiesCollection.ReplaceOneAsync(e => e.Name == _entity.Name, updatedEntity, cancellationToken: ct);

        // requested true, actual true
        status = await shutteringService.ShutteringStatesForService(_entity.Name, "foo.com", ct);
        Assert.NotNull(status);
        Assert.Equal(ShutteringStatus.Shuttered, status.Status);

        // Unshutter the service 
        await shutteringService.Register(new ShutteringRecord("prod", _entity.Name, "foo.com", "", false, new UserDetails(),
            DateTime.Now), ct);

        // requested false, actual true
        status = await shutteringService.ShutteringStatesForService(_entity.Name, "foo.com", ct);
        Assert.NotNull(status);
        Assert.Equal(ShutteringStatus.PendingActive, status.Status);

        // Entity updates with the new status
        updatedEntity.Environments["prod"].Urls["foo.com"].Shuttered = false;
        await entitiesCollection.ReplaceOneAsync(e => e.Name == _entity.Name, updatedEntity, cancellationToken: ct);

        // requested false, actual false
        status = await shutteringService.ShutteringStatesForService(_entity.Name, "foo.com", ct);
        Assert.NotNull(status);
        Assert.Equal(ShutteringStatus.Active, status.Status);
    }


    private readonly Entity _entity = new()
    {
        Name = "foo",
        Teams = [],
        Status = Status.Created,
        Environments = new Dictionary<string, CdpTenant>
        {
            {"prod", new CdpTenant
            {
                Urls = new Dictionary<string, TenantUrl>
                {
                    {"foo.com", new TenantUrl { Shuttered = false, Type = "vanity", Enabled = true }},
                    {"foo.internal.cdp", new TenantUrl { Shuttered = false, Type = "internal", Enabled = true }}
                }
            }}
        }
    };


}