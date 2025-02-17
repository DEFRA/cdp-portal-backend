using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Deployments;

public class DeploymentServiceTest : IClassFixture<MongoIntegrationTest>
{
    readonly MongoIntegrationTest Fixture;

    readonly IRepositoryService repositoryService = Substitute.For<IRepositoryService>();
    readonly IUserServiceFetcher userServiceFetcher = Substitute.For<IUserServiceFetcher>();
    
    public DeploymentServiceTest(MongoIntegrationTest fixture)
    {
        Fixture = fixture;
        Task.Run(() => Fixture.InitializeAsync()).Wait();
    }
    
    [Fact]
    public async Task RegisterDeploymentWithAuditSection()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var service = new DeploymentsServiceV2(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var deployment = DeploymentV2.FromRequest(new RequestedDeployment
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

        repositoryService.FindRepositoryById(deployment.Service, Arg.Any<CancellationToken>())
            .Returns(new Repository
            {
                Id = deployment.Service,
                Teams = [new RepositoryTeam("test-team", "3333", "test team")]
            });
        
        var fullUserDetails = new UserServiceUser("Test User", "test.user@test.com", "9999-9999-9999",
            [new TeamIds("3333", "test team"), new TeamIds("9999", "admins")]);
        userServiceFetcher.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .Returns(new UserServiceUserResponse("success", fullUserDetails));
        
        await service.RegisterDeployment(deployment, new CancellationToken());
        
        var result = await service.FindDeployment(deployment.CdpDeploymentId, new CancellationToken());
        Assert.NotNull(result);
        Assert.Equal([new RepositoryTeam("test-team", "3333", "test team")], result.Audit?.ServiceOwners );
        Assert.Equivalent(fullUserDetails, result.Audit?.User );
    }
    
    [Fact]
    public async Task RegisterDeploymentWhenAuditDataIsUnavailable()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var service = new DeploymentsServiceV2(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var deployment = DeploymentV2.FromRequest(new RequestedDeployment
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

        repositoryService.FindRepositoryById(deployment.Service, Arg.Any<CancellationToken>())
            .ReturnsNull();

        userServiceFetcher.GetUser(deployment.User!.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();
        
        await service.RegisterDeployment(deployment, new CancellationToken());
        
        var result = await service.FindDeployment(deployment.CdpDeploymentId, new CancellationToken());
        Assert.NotNull(result);
        Assert.Equal([], result.Audit?.ServiceOwners );
        Assert.Null(result.Audit?.User );
    }
    
    [Fact]
    public async Task LinkDeployment()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var service = new DeploymentsServiceV2(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var lambdaId = "ecs/12345";
        var deployment = DeploymentV2.FromRequest(new RequestedDeployment
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

        repositoryService.FindRepositoryById(deployment.Service, Arg.Any<CancellationToken>())
            .Returns(new Repository()
            {
                Id = deployment.Service,
                Teams = [new RepositoryTeam("test-team", "3333", "test team")]
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
    public async Task FindWhatsRunningWhereWithNoData()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var service = new DeploymentsServiceV2(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var result = await service.FindWhatsRunningWhere(null, null, null, null, null, new CancellationToken());
        
        Assert.Empty(result);
    }

    
    [Fact]
    public async Task FindWhatsRunningWhereForService()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "deploymentsV2");
        var service = new DeploymentsServiceV2(mongoFactory, repositoryService, userServiceFetcher, new NullLoggerFactory());

        var deployment1 = new DeploymentV2
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
        var deployment2 = new DeploymentV2
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
        
        var result = await service.FindWhatsRunningWhere(deployment1.Service, new CancellationToken());
        // The most recent running service should be shown
        Assert.Single(result);
        Assert.Equal(deployment2.CdpDeploymentId, result[0].CdpDeploymentId);
    }
}
