using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class DeploymentStatusTests
{

    [Fact]
    public void TestUnstableDetector()
    {
        var stable = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "pending", Updated = DateTime.Now }}
            }
        };
        
        var unstable = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new () {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(5)) }},
                {"2", new () {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(4)) }},
                {"3", new () {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(3)) }},
                {"4", new () {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(2)) }},
                {"5", new () {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(1)) }},
                {"6", new () {Status = "pending", Updated = DateTime.Now}}
            }
        };

        var stopped = new DeploymentV2
        {
            InstanceCount = 4, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }},
                {"3", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }},
                {"4", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }}
            }
        };

        
        Assert.False(DeploymentStatus.IsUnstable(stable));
        Assert.True(DeploymentStatus.IsUnstable(unstable));
        Assert.False(DeploymentStatus.IsUnstable(stopped));
    }

    [Fact]
    public void TestOverallStatus()
    {
        var running = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }}
            }
        };
        
        var pending = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"A", new DeploymentInstanceStatus {Status = "pending", Updated = DateTime.Now }},
                {"B", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }}
            }
        };
        
        var stopping = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopping", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }}
            }
        };
        
        var stopped = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now }}
            }
        };
        
        Assert.Equal("running", DeploymentStatus.CalculateOverallStatus(running));
        Assert.Equal("pending", DeploymentStatus.CalculateOverallStatus(pending));
        Assert.Equal("stopping", DeploymentStatus.CalculateOverallStatus(stopping));
        Assert.Equal("stopped", DeploymentStatus.CalculateOverallStatus(stopped));
    }
    
    [Fact]
    public void TestOverallStatusWithFailures()
    {
        var runningWithOldFailure = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromDays(2)) }},
                {"2", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }},
                {"3", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }}
            }
        };
        
        var recoveringFromCrash = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopping", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "pending", Updated = DateTime.Now }}
            }
        };
        
        var crashLoop = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new DeploymentInstanceStatus {Status = "stopping", Updated = DateTime.Now }},
                {"2", new DeploymentInstanceStatus {Status = "running", Updated = DateTime.Now }}
            }
        }; 
        
        for (var i = 0; i < 1000; i++)
        {
            crashLoop.Instances["x" + i] = new DeploymentInstanceStatus { Status = "stopped", Updated = DateTime.Now.Subtract(TimeSpan.FromMinutes(i)) };
        }
        
        Assert.Equal("running", DeploymentStatus.CalculateOverallStatus(runningWithOldFailure));
        Assert.Equal("pending", DeploymentStatus.CalculateOverallStatus(recoveringFromCrash));
        Assert.Equal("running", DeploymentStatus.CalculateOverallStatus(crashLoop));
    }
}