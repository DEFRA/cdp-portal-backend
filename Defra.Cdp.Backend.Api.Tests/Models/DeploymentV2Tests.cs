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
}