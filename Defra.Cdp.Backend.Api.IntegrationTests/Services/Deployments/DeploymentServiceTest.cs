using Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Deployments;

public class DeploymentServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    readonly IUserServiceFetcher userServiceFetcher = Substitute.For<IUserServiceFetcher>();

    [Fact]
    public async Task RegisterDeploymentWithAuditSection()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "DeploymentServiceTest");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        await repositoryService.Upsert(new Repository
        {
            Id = "test-backend",
            Teams = [new RepositoryTeam("test-team", "3333", "test team")],
            CreatedAt = DateTime.Now,
            Description = "",
            Topics = [],
            IsArchived = false,
            PrimaryLanguage = "",
            IsPrivate = false,
            IsTemplate = false,
            Url = "",
        }, new CancellationToken());

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
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User", }
        });

        var fullUserDetails = new UserServiceUser("Test User", "test.user@test.com", "9999-9999-9999",
            [new TeamId("3333", "test team"), new TeamId("9999", "admins")]);
        userServiceFetcher.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .Returns(new UserServiceUserResponse("success", fullUserDetails));

        await service.RegisterDeployment(deployment, new CancellationToken());

        var result = await service.FindDeployment(deployment.CdpDeploymentId, new CancellationToken());
        Assert.NotNull(result);
        Assert.Equal([new RepositoryTeam("test-team", "3333", "test team")], result.Audit?.ServiceOwners);
        Assert.Equivalent(fullUserDetails, result.Audit?.User);
    }

    [Fact]
    public async Task RegisterDeploymentWhenAuditDataIsUnavailable()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

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
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User", }
        });

        userServiceFetcher.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();

        await service.RegisterDeployment(deployment, new CancellationToken());

        var result = await service.FindDeployment(deployment.CdpDeploymentId, new CancellationToken());
        Assert.NotNull(result);
        Assert.Equal([], result.Audit?.ServiceOwners);
        Assert.Null(result.Audit?.User);
    }

    [Fact]
    public async Task LinkDeployment()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

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
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User", }
        });

        var ct = new CancellationToken();
        await service.RegisterDeployment(deployment, new CancellationToken());
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
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

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
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User", }
        });

        var ct = new CancellationToken();
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
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var result = await service.RunningDeploymentsForService(null, null, null, null, null, new CancellationToken());

        Assert.Empty(result);
    }


    [Fact]
    public async Task FindWhatsRunningWhereForService()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

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

        await service.RegisterDeployment(deployment1, new CancellationToken());
        await service.RegisterDeployment(deployment2, new CancellationToken());

        var result = await service.RunningDeploymentsForService(deployment1.Service, new CancellationToken());
        // The most recent running service should be shown
        Assert.Single(result);
        Assert.Equal(deployment2.CdpDeploymentId, result[0].CdpDeploymentId);
    }
    
    [Fact]
    public async Task CleansUpStuckRequestsOnRegister()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var service = new DeploymentsService(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var stuckDeployment = new Deployment
        {
            CdpDeploymentId = "stuck-messge",
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
        await mongoFactory.GetCollection<Deployment>(DeploymentsService.CollectionName)
            .InsertOneAsync(stuckDeployment, new InsertOneOptions(), CancellationToken.None);

        var totalDeployments = await mongoFactory.GetCollection<Deployment>(DeploymentsService.CollectionName)
            .Find(d =>
                d.Service == stuckDeployment.Service &&
                d.Environment == stuckDeployment.Environment &&
                d.Status == DeploymentStatus.Requested).CountDocumentsAsync(CancellationToken.None);
        
        Assert.Equal(1, totalDeployments);
        
        userServiceFetcher.GetUser(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            User = new UserDetails { Id = "9999-9999-9999", DisplayName = "Test User", }
        });
        await service.RegisterDeployment(deployment, new CancellationToken());

        // check its registered the new deployment
        var result = await service.FindDeployment(deployment.CdpDeploymentId, new CancellationToken());
        Assert.NotNull(result);
        Assert.Equal([], result.Audit?.ServiceOwners);
        Assert.Null(result.Audit?.User);
        
        // Check its removed the old requested deployment
        var stuck = await service.FindDeployment(stuckDeployment.CdpDeploymentId, new CancellationToken());
        Assert.Equal(DeploymentStatus.Failed, stuck?.Status);
    }
}