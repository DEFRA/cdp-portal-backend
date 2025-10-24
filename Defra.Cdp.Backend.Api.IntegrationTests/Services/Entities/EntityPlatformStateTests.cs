using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public partial class EntityPlatformStateTests(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    private CdpTenantAndMetadata serviceA = new()
    {
        Metadata = new TenantMetadata
        {
            Type = nameof(Type.Microservice),
            Subtype = nameof(SubType.Backend),
            Teams = ["platform"],
            ServiceCode = "CDP",
            Created = DateTime.UtcNow.ToString()
        },
        Tenant = new CdpTenant
        {
            Urls = new Dictionary<string, TenantUrl>
            {
                { "internal", new TenantUrl { Type = "internal", Enabled = false, Shuttered = false } }
            },
            Logs = new OpensearchDashboard { Name = "logs", Url = "http://logs/tenant" },
            TenantConfig = new RequestedConfig { Zone = "protected", Mongo = true, Redis = false, }
        }
    };

    [Fact]
    public async Task Create_entity_if_missing()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        Assert.Null(await service.GetEntity("service-a", CancellationToken.None));


        var state = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "test",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                { "service-a", serviceA }
            }
        };

        await service.UpdateEnvironmentState(state, CancellationToken.None);

        var result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(Type.Microservice, result.Type);
        Assert.Equal(SubType.Backend, result.SubType);
        Assert.Equal(Status.Creating, result.Status);
        Assert.True(result.Envs.ContainsKey("test"));
        Assert.Equivalent(serviceA.Tenant, result.Envs["test"]);
    }

    [Fact]
    public async Task Update_entity_if_it_exists()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        await service.Create(new Entity { Name = "service-a", SubType = SubType.Frontend, Type = Type.Microservice, Status = Status.Created },
            CancellationToken.None);

        Assert.NotNull(await service.GetEntity("service-a", CancellationToken.None));

        var state = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "test",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                { "service-a", serviceA }
            }
        };

        await service.UpdateEnvironmentState(state, CancellationToken.None);

        var result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(Type.Microservice, result.Type);
        Assert.Equal(SubType.Backend, result.SubType);
        Assert.True(result.Envs.ContainsKey("test"));
        Assert.Equivalent(serviceA.Tenant, result.Envs["test"]);
    }

    [Fact]
    public async Task Unsets_existing_env_if_removed_from_payload()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var state = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "test",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                { "service-a", serviceA }
            }
        };

        await service.UpdateEnvironmentState(state, CancellationToken.None);
        var result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.Envs.ContainsKey("test"));

        var nextState = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "test",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>()
        };

        await service.UpdateEnvironmentState(nextState, CancellationToken.None);
        result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.Envs.ContainsKey("test"));
    }


    [Fact]
    public async Task Updates_multiple_environments()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var testState = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "test",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                { "service-a", serviceA }
            }
        };
        await service.UpdateEnvironmentState(testState, CancellationToken.None);

        var devState = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "dev",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>{
                { "service-a", serviceA }
            }
        };
        await service.UpdateEnvironmentState(devState, CancellationToken.None);

        var result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.Envs.ContainsKey("test"));
        Assert.True(result.Envs.ContainsKey("dev"));
    }
    
    [Fact]
    public async Task Tenant_with_limited_environments_has_correct_status()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, GetType().Name);
        var service = new EntitiesService(mongoFactory, new NullLoggerFactory());

        var testState = new PlatformStatePayload
        {
            Version = 1,
            TerraformSerials = new Serials(),
            Environment = "management",
            Tenants = new Dictionary<string, CdpTenantAndMetadata>
            {
                { "service-a",  new CdpTenantAndMetadata {
                        Tenant = serviceA.Tenant,
                        Metadata = new TenantMetadata
                        {
                            Type = nameof(Type.Microservice),
                            Subtype = nameof(SubType.Backend),
                            Teams = ["platform"],
                            ServiceCode = "CDP",
                            Created = DateTime.UtcNow.ToString(),
                            Environments = ["management"]
                        },
                        Progress = new CreationProgress
                        {
                            Complete = true
                        }
                    }
                }
            }
        };
        await service.UpdateEnvironmentState(testState, CancellationToken.None);
        await service.BulkUpdateCreationStatus(CancellationToken.None);
        var result = await service.GetEntity("service-a", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(CdpEnvironments.EnvironmentIds.Length, result.Progress.Count);
        Assert.True(result.Progress.Values.All(v => v.Complete));
        Assert.Equal(Status.Created, result.Status);
    }
}