using Defra.Cdp.Backend.Api.Services.Aws.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class GenerateStatusTests
{
    private const string RUNNING = "RUNNING";
    private const string PENDING = "PENDING";
    private const string PROVISIONING = "PROVISIONING";
    private const string STOPPED = "STOPPED";
    private const string STOPPING = "STOPPING";
    
    [Fact]
    public void TestValidStatusPairs()
    {
        string[][] data =
        {
            //      desired  last    expected
            new[] { RUNNING, RUNNING, "in-progress" }, 
            new[] { RUNNING, PENDING, "starting" },
            new[] { RUNNING, PROVISIONING, "starting" },
            new[] { RUNNING, STOPPED, "failed" },
            new[] { STOPPED, STOPPING, "stopping" },
            new[] { STOPPED, RUNNING, "stopping" },
            new[] { STOPPED, PENDING, "stopping" },
            new[] { STOPPED, PROVISIONING, "stopping" },
            new[] { STOPPED, STOPPED, "finished" }
        };

        foreach (var testcase in data)
        {
            Assert.Equal(DeploymentEventHandlerV2.GenerateTestSuiteTaskStatus(testcase[0], testcase[1]), testcase[2]);
        }
    }
}