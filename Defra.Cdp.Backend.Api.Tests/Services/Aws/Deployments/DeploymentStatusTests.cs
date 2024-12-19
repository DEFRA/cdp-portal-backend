using Defra.Cdp.Backend.Api.Models;
using Org.BouncyCastle.Tls;
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
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
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
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
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
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_COMPLETED
        };

        
        Assert.False(IsUnstable(stable));
        Assert.True(IsUnstable(unstable));
        Assert.False(IsUnstable(stopped));
    }

    [Fact]
    public void TestOverallStatus()
    {
        var deploymentRequestedWithoutAnyInstances = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>(),
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS,
            Status = Requested
        };
        var runningWithoutDeploymentComplete = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Running, DateTime.Now )},
                {"2", new (Running, DateTime.Now )}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        };
        
        var runningWithoutDeploymentStatus = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Running, DateTime.Now )},
                {"2", new (Running, DateTime.Now )}
            }
        };
        
        var runningWithDeploymentComplete = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Running, DateTime.Now )},
                {"2", new (Running, DateTime.Now )}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_COMPLETED
        };
        
        var pending = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"A", new ( Pending, DateTime.Now )},
                {"B", new ( Running, DateTime.Now )}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        };
        
        var stopping = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopping, DateTime.Now)},
                {"2", new (Running, DateTime.Now)}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        };
        
        var stopped = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopped, DateTime.Now)},
                {"2", new (Stopped,DateTime.Now)}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_COMPLETED
        };
        
        var failed = new DeploymentV2
        {
            InstanceCount = 2, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopped, DateTime.Now)},
                {"2", new (Stopped,DateTime.Now)}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_FAILED
        };
        
        Assert.Equal(Requested, CalculateOverallStatus(deploymentRequestedWithoutAnyInstances));
        Assert.Equal(Pending, CalculateOverallStatus(runningWithoutDeploymentComplete));
        Assert.Equal(Pending, CalculateOverallStatus(runningWithoutDeploymentStatus));
        Assert.Equal(Running, CalculateOverallStatus(runningWithDeploymentComplete));
        Assert.Equal(Pending, CalculateOverallStatus(pending));
        Assert.Equal(Stopping, CalculateOverallStatus(stopping));
        Assert.Equal(Stopped, CalculateOverallStatus(stopped));
        Assert.Equal(Failed, CalculateOverallStatus(failed));
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
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_COMPLETED
        };
        
        var recoveringFromCrash = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new (Stopping, DateTime.Now)},
                {"2", new (Pending, DateTime.Now)}
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        };
        
        var crashLoop = new DeploymentV2
        {
            InstanceCount = 1, 
            Instances = new Dictionary<string, DeploymentInstanceStatus>
            {
                {"1", new(Stopping,DateTime.Now) },
                {"2", new(Running, DateTime.Now) }
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        }; 
        
        for (var i = 0; i < 1000; i++)
        {
            crashLoop.Instances["x" + i] = new DeploymentInstanceStatus(Stopped, DateTime.Now.Subtract(TimeSpan.FromMinutes(i)));
        }
        
        Assert.Equal(Running, CalculateOverallStatus(runningWithOldFailure));
        Assert.Equal(Pending, CalculateOverallStatus(recoveringFromCrash));
        Assert.Equal(Pending, CalculateOverallStatus(crashLoop));
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
            },
            LastDeploymentStatus = SERVICE_DEPLOYMENT_IN_PROGRESS
        };
        
        Assert.Equal(Pending, CalculateOverallStatus(running));
    }
}