using Defra.Cdp.Backend.Api.Models;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

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
                {"1", new ( Running, DateTime.Now) },
                {"2", new ( Pending, DateTime.Now) }
            }
        };
        
        var unstable = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(5)) )},
                {"2", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(4)) )},
                {"3", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(3)) )},
                {"4", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(2)) )},
                {"5", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)) )},
                {"6", new (Pending, DateTime.Now)}
            }
        };

        var stopped = new DeploymentV2
        {
            InstanceCount = 4, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new ( Stopped, DateTime.Now) },
                {"2", new ( Stopped, DateTime.Now) },
                {"3", new ( Stopped, DateTime.Now) },
                {"4", new ( Stopped, DateTime.Now) }
            }
        };

        
        Assert.False(IsUnstable(stable));
        Assert.True(IsUnstable(unstable));
        Assert.False(IsUnstable(stopped));
    }

    [Fact]
    public void TestOverallStatus()
    {
        var running = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Running, DateTime.Now )},
                {"2", new (Running, DateTime.Now )}
            }
        };
        
        var pending = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"A", new ( Pending, DateTime.Now )},
                {"B", new ( Running, DateTime.Now )}
            }
        };
        
        var stopping = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopping, DateTime.Now)},
                {"2", new (Running, DateTime.Now)}
            }
        };
        
        var stopped = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopped, DateTime.Now)},
                {"2", new (Stopped,DateTime.Now)}
            }
        };
        
        Assert.Equal(Running, CalculateOverallStatus(running));
        Assert.Equal(Pending, CalculateOverallStatus(pending));
        Assert.Equal(Stopping, CalculateOverallStatus(stopping));
        Assert.Equal(Stopped, CalculateOverallStatus(stopped));
    }
    
    [Fact]
    public void TestOverallStatusWithFailures()
    {
        var runningWithOldFailure = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopped, DateTime.Now.Subtract(TimeSpan.FromDays(2)))},
                {"2", new (Running, DateTime.Now)},
                {"3", new (Running, DateTime.Now) }
            }
        };
        
        var recoveringFromCrash = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopping, DateTime.Now)},
                {"2", new (Pending, DateTime.Now)}
            }
        };
        
        var crashLoop = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new(Stopping,DateTime.Now) },
                {"2", new(Running, DateTime.Now) }
            }
        }; 
        
        for (var i = 0; i < 1000; i++)
        {
            crashLoop.Instances["x" + i] = new DeploymentInstanceStatus(Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(i)));
        }
        
        Assert.Equal(Running, CalculateOverallStatus(runningWithOldFailure));
        Assert.Equal(Pending, CalculateOverallStatus(recoveringFromCrash));
        Assert.Equal(Running, CalculateOverallStatus(crashLoop));
    }
    
    [Fact]
    public void TestOverallStatusWhenInstancesComeUpSlow()
    {
        var running = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Running, DateTime.Now )}
            }
        };
        
        Assert.Equal(Pending, CalculateOverallStatus(running));
    }
}