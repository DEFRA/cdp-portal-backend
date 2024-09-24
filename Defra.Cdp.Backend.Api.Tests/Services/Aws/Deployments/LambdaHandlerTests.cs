using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class LambdaHandlerTests
{
    [Fact]
    public async Task TestLambdaHandlerLinksExistingDeployment()
    {
        var service = Substitute.For<IDeploymentsServiceV2>();
        var handler = new LambdaMessageHandlerV2(service, new NullLogger<LambdaMessageHandlerV2>());
        
        
        var lambdaEvent = new EcsDeploymentLambdaEvent(
            "ECS Lambda Deployment Created",
            "00000000",
            new EcsDeploymentLambdaDetail("INFO", "CREATED", "ecs-svc/5730707953135730843", "reason"),
            "12345678",
            new EcsDeploymentLambdaRequest(
                ContainerImage: "cdp-portal-backend",
                ContainerVersion: "0.1.0",
                DesiredCount: 1,
                EnvFiles: [new EcsConfigFile("arn:aws:s3:::cdp-management-service-configs/e695d47d5d5a9bd9519b0b4c412c79f052d2c35a/global/global_fixed.env", "s3")],
                TaskCpu: 1024,
                TaskMemory: 2048,
                Environment: "infra-dev",
                Zone: "protected",
                DeployedBy: new EcsDeployedBy("12345678", "0", "test user"),
                ServiceCode: "CDP"
            )
        );
   
        // setup mocks
        var lookupResult = Task.FromResult<DeploymentV2?>(null); 
        service.FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(lookupResult);
        service.LinkDeployment(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        service.UpdateDeploymentStatus(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        
        await handler.Handle("1",  lambdaEvent, CancellationToken.None);

        await service.Received().FindDeploymentByLambdaId("ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.Received().LinkDeployment("12345678", "ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.Received().UpdateDeploymentStatus("ecs-svc/5730707953135730843", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task TestLambdaHandlerNotUpdateAnExistingLink()
    {
        var service = Substitute.For<IDeploymentsServiceV2>();
        var handler = new LambdaMessageHandlerV2(service, new NullLogger<LambdaMessageHandlerV2>());
        
        var lambdaEvent = new EcsDeploymentLambdaEvent(
            "ECS Lambda Deployment Created",
            "00000000",
            new EcsDeploymentLambdaDetail("INFO", "CREATED", "ecs-svc/5730707953135730843", "reason"),
            "12345678",
            new EcsDeploymentLambdaRequest(
                ContainerImage: "cdp-portal-backend",
                ContainerVersion: "0.1.0",
                DesiredCount: 1,
                EnvFiles: [new EcsConfigFile("arn:aws:s3:::cdp-management-service-configs/e695d47d5d5a9bd9519b0b4c412c79f052d2c35a/global/global_fixed.env", "s3")],
                TaskCpu: 1024,
                TaskMemory: 2048,
                Environment: "infra-dev",
                Zone: "protected",
                DeployedBy: new EcsDeployedBy("12345678", "0", "test user"),
                ServiceCode: "CDP"
            )
        );
   
        // setup mocks
        var lookupResult = Task.FromResult<DeploymentV2?>(new DeploymentV2()); 
        service.FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(lookupResult);
        service.LinkDeployment(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        service.UpdateDeploymentStatus(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        
        await handler.Handle("1",  lambdaEvent, CancellationToken.None);

        await service.Received().FindDeploymentByLambdaId("ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.DidNotReceive().LinkDeployment("12345678", "ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.Received().UpdateDeploymentStatus("ecs-svc/5730707953135730843", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task TestLambdaHandlerCreateMissingDeployment()
    {
        // This would happen when the deployment originated from a different deployment of the portal
        
        var service = Substitute.For<IDeploymentsServiceV2>();
        var handler = new LambdaMessageHandlerV2(service, new NullLogger<LambdaMessageHandlerV2>());
        
        var lambdaEvent = new EcsDeploymentLambdaEvent(
            "ECS Lambda Deployment Created",
            "00000000",
            new EcsDeploymentLambdaDetail("INFO", "CREATED", "ecs-svc/5730707953135730843", "reason"),
            "12345678",
            new EcsDeploymentLambdaRequest(
                ContainerImage: "cdp-portal-backend",
                ContainerVersion: "0.1.0",
                DesiredCount: 1,
                EnvFiles: [new EcsConfigFile("arn:aws:s3:::cdp-management-service-configs/e695d47d5d5a9bd9519b0b4c412c79f052d2c35a/global/global_fixed.env", "s3")],
                TaskCpu: 1024,
                TaskMemory: 2048,
                Environment: "infra-dev",
                Zone: "protected",
                DeployedBy: new EcsDeployedBy("12345678", "0", "test user"),
                ServiceCode: "CDP"
            )
        );
   
        // setup mocks
        var lookupResult = Task.FromResult<DeploymentV2?>(null); 
        service.FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(lookupResult);
        
        // fail linking as there's no existing deployment
        service.LinkDeployment(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        service.RegisterDeployment(Arg.Any<DeploymentV2>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        
        service.UpdateDeploymentStatus(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        
        await handler.Handle("1",  lambdaEvent, CancellationToken.None);

        await service.Received().FindDeploymentByLambdaId("ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.Received().LinkDeployment("12345678", "ecs-svc/5730707953135730843", Arg.Any<CancellationToken>());
        await service.Received().RegisterDeployment(Arg.Any<DeploymentV2>(), Arg.Any<CancellationToken>());
        await service.Received().UpdateDeploymentStatus("ecs-svc/5730707953135730843", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}