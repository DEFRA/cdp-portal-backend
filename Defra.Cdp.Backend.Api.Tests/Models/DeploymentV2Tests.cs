using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Models;
using static DeploymentStatus;

public class DeploymentV2Tests
{
    [Fact]
    public void TestTrimInstances()
    {
        var deployment = new DeploymentV2
        {
            InstanceCount = 1
        };
        
        for (var i = 1; i < 21; i++)
        {
            deployment.Instances["id-" + i] = new DeploymentInstanceStatus(Stopped, new DateTime().AddMinutes(i));
        }
        deployment.Instances["id-0"] = new DeploymentInstanceStatus(Running, new DateTime());
        
        Assert.Equal(21, deployment.Instances.Count);
        deployment.TrimInstance(20);
        Assert.Equal(20, deployment.Instances.Count);
        Assert.True(deployment.Instances.ContainsKey("id-0"));
        Assert.False(deployment.Instances.ContainsKey("id-1"));
    }
    
    [Fact]
    public void TestTrimInstancesWhenNoneShouldBeRemoved()
    {
        var deployment = new DeploymentV2
        {
            InstanceCount = 1
        };
        
        for (var i = 1; i < 21; i++)
        {
            deployment.Instances["id-" + i] = new DeploymentInstanceStatus(Stopped, new DateTime().AddMinutes(i));
        }
        deployment.Instances["id-0"] = new DeploymentInstanceStatus(Running, new DateTime());
        
        Assert.Equal(21, deployment.Instances.Count);
        deployment.TrimInstance(200);
        Assert.Equal(21, deployment.Instances.Count);
    }

    [Fact]
    public void TestExtractCommitSha()
    {
        var input =
            "arn:aws:s3:::cdp-management-service-configs/e695d47d5d5a9bd9519b0b4c412c79f052d2c35a/global/global_fixed.env";
        var result = DeploymentV2.ExtractCommitSha(input);
        
        Assert.Equal("e695d47d5d5a9bd9519b0b4c412c79f052d2c35a", result);
    }
    
    [Fact]
    public void TestExtractCommitShaWhenDataIsInvalid()
    {
        var input =
            "arn:aws:s3:::cdp-management-service-configs/global/global_fixed.env";
        var result = DeploymentV2.ExtractCommitSha(input);
        
        Assert.Null(result);
    }

    [Fact]
    public void TestCreteDeploymentFromLambda()
    {
        var lambda = new EcsDeploymentLambdaEvent(
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
                DeployedBy: new EcsDeployedBy("0", "test user")
            )
        );

        var deployment = DeploymentV2.FromLambdaMessage(lambda);
        Assert.NotNull(deployment);
        Assert.Equal(1, deployment.InstanceCount);
        Assert.Equal("12345678", deployment.CdpDeploymentId);
        Assert.Equal("cdp-portal-backend", deployment.Service);
        Assert.Equal("0.1.0", deployment.Version);
        Assert.Equal("1024", deployment.Cpu);
        Assert.Equal("2048", deployment.Memory);
        Assert.Equal("ecs-svc/5730707953135730843", deployment.LambdaId);
        Assert.Equal("e695d47d5d5a9bd9519b0b4c412c79f052d2c35a", deployment.ConfigVersion);
    }
}