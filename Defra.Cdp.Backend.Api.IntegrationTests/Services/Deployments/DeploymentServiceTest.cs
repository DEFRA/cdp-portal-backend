using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Deployments;

public class DeploymentServiceTest : ServiceTest
{
    private readonly IUserServiceBackendClient _userServiceBackendClient = Substitute.For<IUserServiceBackendClient>();
    private readonly MongoDbClientFactory _mongoFactory;

    public DeploymentServiceTest(MongoContainerFixture fixture) : base(fixture)
    {
        _mongoFactory = CreateMongoDbClientFactory(); ;
    }


    [Fact]
    public async Task RegisterDeploymentWithAuditSection()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        await repositoryService.Create(new Entity
        {
            Name = "test-backend",
            Teams = [new Team { Name = "test-team", TeamId = "test team" }],
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend
        }, TestContext.Current.CancellationToken);

        var deployment = Deployment.FromRequest(new RequestedDeployment
        {
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "e5fa44f2b31c1fb553b6021e7360d07d5d91ff5e",
            Environment = "test",
            DeploymentId = Guid.NewGuid().ToString(),
            InstanceCount = 1,
            Service = "test-backend",
            Version = "1.0.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User" }
        });

        var fullUserDetails = new UserServiceUser("Test User", "test.user@test.com", "9999-9999-9999",
            [new Models.TeamIdAndName("3333", "test team"), new Models.TeamIdAndName("9999", "admins")]);
        _userServiceBackendClient.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .Returns(fullUserDetails);

        await service.RegisterDeployment(deployment, TestContext.Current.CancellationToken);

        var result = await service.FindDeployment(deployment.CdpDeploymentId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([new Team { Name = "test-team", TeamId = "test team" }], result.Audit?.ServiceOwners);
        Assert.Equivalent(fullUserDetails, result.Audit?.User);
    }

    [Fact]
    public async Task RegisterDeploymentWhenAuditDataIsUnavailable()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        var deployment = Deployment.FromRequest(new RequestedDeployment
        {
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "e5fa44f2b31c1fb553b6021e7360d07d5d91ff5e",
            Environment = "test",
            DeploymentId = Guid.NewGuid().ToString(),
            InstanceCount = 1,
            Service = "test-backend",
            Version = "1.0.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User" }
        });

        _userServiceBackendClient.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();

        await service.RegisterDeployment(deployment, TestContext.Current.CancellationToken);

        var result = await service.FindDeployment(deployment.CdpDeploymentId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([], result.Audit?.ServiceOwners);
        Assert.Null(result.Audit?.User);
    }

    [Fact]
    public async Task LinkDeployment()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        const string lambdaId = "ecs/12345";
        var deployment = Deployment.FromRequest(new RequestedDeployment
        {
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "e5fa44f2b31c1fb553b6021e7360d07d5d91ff5e",
            Environment = "test",
            DeploymentId = Guid.NewGuid().ToString(),
            InstanceCount = 1,
            Service = "link-test-backend",
            Version = "1.0.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User" }
        });

        var ct = TestContext.Current.CancellationToken;
        await service.RegisterDeployment(deployment, TestContext.Current.CancellationToken);
        var linked = await service.LinkDeployment(deployment.CdpDeploymentId, lambdaId, ct);
        Assert.True(linked);

        var result = await service.FindDeployment(deployment.CdpDeploymentId, ct);
        Assert.NotNull(result);
        Assert.Equal(lambdaId, result.LambdaId);
        Assert.Equal(deployment.CdpDeploymentId, result.CdpDeploymentId);

        var resultByLambdaId = await service.FindDeploymentByLambdaId(lambdaId, ct);
        Assert.NotNull(resultByLambdaId);
        Assert.Equal(deployment.CdpDeploymentId, resultByLambdaId.CdpDeploymentId);
    }

    [Fact]
    public async Task UpdateDeploymentInstance()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        const string lambdaId = "ecs/12345";
        var deployment = Deployment.FromRequest(new RequestedDeployment
        {
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "e5fa44f2b31c1fb553b6021e7360d07d5d91ff5e",
            Environment = "test",
            DeploymentId = Guid.NewGuid().ToString(),
            InstanceCount = 2,
            Service = "link-test-backend",
            Version = "1.0.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User" }
        });

        var ct = TestContext.Current.CancellationToken;
        await service.RegisterDeployment(deployment, ct);
        var linked = await service.LinkDeployment(deployment.CdpDeploymentId, lambdaId, ct);
        Assert.True(linked);

        var instance1 = new DeploymentInstanceStatus(DeploymentStatus.Running, DateTime.Now);
        var instance2 = new DeploymentInstanceStatus(DeploymentStatus.Pending, DateTime.Now);
        await service.UpdateInstance(deployment.CdpDeploymentId, "instance1", instance1, ct);
        await service.UpdateInstance(deployment.CdpDeploymentId, "instance2", instance2, ct);

        var result = await service.FindDeployment(deployment.CdpDeploymentId, ct);
        Assert.NotNull(result);
        Assert.Equal(lambdaId, result.LambdaId);
        Assert.Equal(deployment.CdpDeploymentId, result.CdpDeploymentId);
        Assert.Equivalent(instance1.Status, result.Instances["instance1"].Status);
        Assert.Equivalent(instance2.Status, result.Instances["instance2"].Status);
    }

    [Fact]
    public async Task FindWhatsRunningWhereWithNoData()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        var result = await service.RunningDeploymentsForService(new DeploymentMatchers(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }


    [Fact]
    public async Task FindWhatsRunningWhereForService()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        var deployment1 = new Deployment
        {
            CdpDeploymentId = Guid.NewGuid().ToString(),
            Environment = "test",
            Service = "test-backend",
            Version = "1.0.0",
            Created = DateTime.Now.AddDays(-1).AddMinutes(-30),
            Updated = DateTime.Now.AddDays(-1),
            Instances =
            {
                {"instance-1111", new DeploymentInstanceStatus(DeploymentStatus.Stopped, DateTime.Now.AddDays(-1))}
            },
            Status = DeploymentStatus.Stopped
        };
        var deployment2 = new Deployment
        {
            CdpDeploymentId = Guid.NewGuid().ToString(),
            Environment = "test",
            Service = "test-backend",
            Version = "1.1.0",
            Created = DateTime.Now.AddMinutes(-30),
            Updated = DateTime.Now,
            Instances =
            {
                {"instance-2222", new DeploymentInstanceStatus(DeploymentStatus.Stopped, DateTime.Now)}
            },
            Status = DeploymentStatus.Running
        };

        await service.RegisterDeployment(deployment1, TestContext.Current.CancellationToken);
        await service.RegisterDeployment(deployment2, TestContext.Current.CancellationToken);

        var result = await service.RunningDeploymentsForService(deployment1.Service, TestContext.Current.CancellationToken);
        // The most recent running service should be shown
        Assert.Single(result);
        Assert.Equal(deployment2.CdpDeploymentId, result[0].CdpDeploymentId);
    }

    [Fact]
    public async Task CleansUpStuckRequestsOnRegister()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());

        var stuckDeployment = new Deployment
        {
            CdpDeploymentId = "stuck-message",
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "6bdb2e40b71f65edff09b9e41474780b9c6426d83d9bfa53376025f89260fbf8",
            Environment = "test",
            InstanceCount = 1,
            Service = "test-backend",
            Version = "0.31.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Stuck user" },
            Created = DateTime.Now.Subtract(TimeSpan.FromDays(30)),
            Updated = DateTime.Now.Subtract(TimeSpan.FromDays(30)),
            Status = DeploymentStatus.Requested
        };
        await _mongoFactory.GetCollection<Deployment>(DeploymentsService.CollectionName)
            .InsertOneAsync(stuckDeployment, new InsertOneOptions(), TestContext.Current.CancellationToken);

        var totalDeployments = await _mongoFactory.GetCollection<Deployment>(DeploymentsService.CollectionName)
            .Find(d =>
                d.Service == stuckDeployment.Service &&
                d.Environment == stuckDeployment.Environment &&
                d.Status == DeploymentStatus.Requested).CountDocumentsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, totalDeployments);

        _userServiceBackendClient.GetUser(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        var deployment = Deployment.FromRequest(new RequestedDeployment
        {
            Cpu = "1024",
            Memory = "1024",
            ConfigVersion = "e5fa44f2b31c1fb553b6021e7360d07d5d91ff5e",
            Environment = "test",
            DeploymentId = Guid.NewGuid().ToString(),
            InstanceCount = 1,
            Service = "test-backend",
            Version = "1.0.0",
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User" }
        });
        await service.RegisterDeployment(deployment, TestContext.Current.CancellationToken);

        // check its registered the new deployment
        var result = await service.FindDeployment(deployment.CdpDeploymentId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal([], result.Audit?.ServiceOwners);
        Assert.Null(result.Audit?.User);

        // Check its removed the old requested deployment
        var stuck = await service.FindDeployment(stuckDeployment.CdpDeploymentId, TestContext.Current.CancellationToken);
        Assert.Equal(DeploymentStatus.Failed, stuck?.Status);
    }

    [Fact]
    public async Task FindDeployments()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());
        var ct = TestContext.Current.CancellationToken;
        await DeploymentsTestHelpers.PopulateWithTestData(service, ct);

        // partial service match
        var results = await service.FindLatest(new DeploymentMatchers(Service: "foo"), ct: ct);
        Assert.True(results.Data.All(d => d.Service.Contains("foo")));

        // exact username match
        results = await service.FindLatest(new DeploymentMatchers(User: "user-1"), ct: ct);
        Assert.True(results.Data.All(d => d.User?.DisplayName == "user-1"));

        // exact user id match
        results = await service.FindLatest(new DeploymentMatchers(User: "1"), ct: ct);
        Assert.True(results.Data.All(d => d.User?.Id == "1"));

        // single environment match
        results = await service.FindLatest(new DeploymentMatchers(Environment: "test"), ct: ct);
        Assert.True(results.Data.All(d => d.Environment == "test"));

        // Multi environment match
        results = await service.FindLatest(new DeploymentMatchers(Environments: ["test", "dev"]), ct: ct);
        Assert.True(results.Data.All(d => d.Environment is "test" or "dev"));

        // multiservice match
        results = await service.FindLatest(new DeploymentMatchers(Services: ["foo-backend", "foo-frontend"]), ct: ct);
        Assert.True(results.Data.All(d => d.Service is "foo-backend" or "foo-frontend"));

        // status
        results = await service.FindLatest(new DeploymentMatchers(Status: DeploymentStatus.Pending), ct: ct);
        Assert.True(results.Data.All(d => d.Status is DeploymentStatus.Pending));
    }


    [Fact]
    public async Task GetDeploymentsFilters()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service =
            new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());
        var ct = TestContext.Current.CancellationToken;
        var deployments = await DeploymentsTestHelpers.PopulateWithTestData(service, ct);

        var services = deployments.Select(d => d.Service).Distinct();
        var users = deployments.Select(d => d.User).Distinct();

        var filters = await service.GetDeploymentsFilters(ct);

        Assert.Equivalent(services, filters.Services);
        Assert.Equivalent(users, filters.Users);
    }

    [Fact]
    public async Task GetWhatsRunningWhereFilters()
    {
        var repositoryService = new EntitiesService(_mongoFactory, new NullLoggerFactory());
        var service =
            new DeploymentsService(_mongoFactory, repositoryService, _userServiceBackendClient, new NullLoggerFactory());
        var ct = TestContext.Current.CancellationToken;
        var deployments = await DeploymentsTestHelpers.PopulateWithTestData(service, ct);

        var services = deployments.Select(d => d.Service).Distinct();
        var users = deployments.Select(d => d.User).Distinct();

        var filters = await service.GetWhatsRunningWhereFilters(ct);

        Assert.Equivalent(services, filters.Services);
        Assert.Equivalent(users, filters.Users);
    }
}