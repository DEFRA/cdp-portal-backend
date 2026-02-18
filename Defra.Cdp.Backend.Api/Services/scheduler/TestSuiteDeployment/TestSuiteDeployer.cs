using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.scheduler.TestSuiteDeployment;

public interface ITestSuiteDeployer
{
    Task DeployAsync(string testSuite, string environment, int cpu, int memory, string? profile, CancellationToken ct);
}

public class TestSuiteDeployer(ISelfServiceOpsClient selfServiceOpsClient, ILogger<TestSuiteDeployer> logger)
    : ITestSuiteDeployer
{
    private readonly UserDetails _userDetails = new()
    {
        Id = "00000000-0000-0000-0000-00000000001", DisplayName = "Auto schedule"
    };

    public async Task DeployAsync(
        string testSuite,
        string environment,
        int cpu,
        int memory,
        string? profile,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "Deploying test-suite {testSuite} to {environment} with profile {profile}",
            testSuite, environment, profile
        );

        await selfServiceOpsClient.TriggerTestSuite(testSuite, _userDetails, environment,
            new TestRunSettings { Cpu = cpu, Memory = memory, }, profile, null, ct);

        logger.LogInformation("Deployment of test-suite {testSuite} to {environment} with profile {profile} complete",
            testSuite, environment, profile);
    }
}