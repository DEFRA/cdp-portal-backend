using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Deployments;

public static class DeploymentsTestHelpers
{
    public static async Task<List<Deployment>> PopulateWithTestData(IDeploymentsService deploymentsService, CancellationToken ct)
    {
        var now = DateTime.Now;
        var user1 = new UserDetails { Id = "1", DisplayName = "user one" };
        var user2 = new UserDetails { Id = "2", DisplayName = "user two" };
        var deployments = new List<Deployment>
        {
            Generate(now.Subtract(TimeSpan.FromDays(1)), "foo-backend", "0.9.0", "dev", DeploymentStatus.Running, user1),
            Generate(now.Subtract(TimeSpan.FromDays(2)), "foo-backend", "0.8.0", "dev", DeploymentStatus.Stopped, user2),
            Generate(now.Subtract(TimeSpan.FromDays(1)), "foo-backend", "0.9.0", "test", DeploymentStatus.Running, user1),
            Generate(now.Subtract(TimeSpan.FromDays(2)), "foo-backend", "0.8.0", "test", DeploymentStatus.Stopped, user2),
            Generate(now.Subtract(TimeSpan.FromDays(1)), "foo-frontend", "1.1.0", "dev", DeploymentStatus.Running, user2),
            Generate(now.Subtract(TimeSpan.FromDays(2)), "foo-frontend", "1.1.0", "dev", DeploymentStatus.Stopped, user2),
            Generate(now.Subtract(TimeSpan.FromDays(1)), "foo-frontend", "1.0.0", "test", DeploymentStatus.Pending, user1)
        };

        foreach (var deployment in deployments)
        {
            await deploymentsService.RegisterDeployment(deployment, ct);
        }

        return deployments;
    }

    public static Deployment Generate(DateTime date, string service, string version, string env, string status, UserDetails user)
    {
        return new Deployment
        {
            CdpDeploymentId = Guid.NewGuid().ToString(),
            Audit = null,
            Created = date,
            Environment = env,
            InstanceCount = 1,
            User = user,
            Secrets = new TenantSecretKeys(),
            Updated = date,
            Version = version,
            Status = status,
            Unstable = false
        };
    }
}