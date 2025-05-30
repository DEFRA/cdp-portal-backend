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
        [
            //      desired  last    expected
            [RUNNING, RUNNING, "in-progress"],
            [RUNNING, PENDING, "starting"],
            [RUNNING, PROVISIONING, "starting"],
            [RUNNING, STOPPED, "failed"],
            [STOPPED, STOPPING, "stopping"],
            [STOPPED, RUNNING, "stopping"],
            [STOPPED, PENDING, "stopping"],
            [STOPPED, PROVISIONING, "stopping"],
            [STOPPED, STOPPED, "finished"]
        ];

        foreach (var testcase in data)
        {
            Assert.Equal(testcase[2], TaskStateChangeEventHandler.GenerateTestSuiteTaskStatus(testcase[0], testcase[1], false));
        }

        foreach (var testcase in data)
        {
            Assert.Equal("failed", TaskStateChangeEventHandler.GenerateTestSuiteTaskStatus(testcase[0], testcase[1], true));
        }
    }
}