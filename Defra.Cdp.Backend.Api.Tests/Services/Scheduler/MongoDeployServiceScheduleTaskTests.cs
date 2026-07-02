using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Scheduler;

public class MongoDeployServiceScheduleTaskTests
{
    [Fact]
    public async Task Should_deploy_to_each_environment_when_due_and_artifact_exists()
    {
        var artifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentExecutor = Substitute.For<IServiceDeploymentExecutor>();
        var serviceProvider = CreateProvider(artifactsService, deploymentExecutor);

        artifactsService.FindLatest("cdp-canary-deployment-backend", Arg.Any<CancellationToken>())
            .Returns(new DeployableArtifact
            {
                Repo = "cdp-canary-deployment-backend",
                Tag = "1.2.3",
                Sha256 = "sha256:abc"
            });

        var task = new MongoDeployServiceScheduleTask
        {
            EntityId = "cdp-canary-deployment-backend",
            Environments = ["infra-dev", "prod"]
        };

        await task.ExecuteAsync(
            serviceProvider,
            DateTime.UtcNow,
            NullLogger<object>.Instance,
            CancellationToken.None);

        await deploymentExecutor.Received(1).DeployAsync(
            "cdp-canary-deployment-backend",
            "1.2.3",
            "infra-dev",
            Arg.Is<UserDetails>(u => u.Id == "00000000-0000-0000-0000-00000000001"),
            Arg.Any<CancellationToken>());

        await deploymentExecutor.Received(1).DeployAsync(
            "cdp-canary-deployment-backend",
            "1.2.3",
            "prod",
            Arg.Is<UserDetails>(u => u.DisplayName == "Auto schedule"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_execute_when_next_run_is_too_old()
    {
        var artifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentExecutor = Substitute.For<IServiceDeploymentExecutor>();
        var serviceProvider = CreateProvider(artifactsService, deploymentExecutor);

        var task = new MongoDeployServiceScheduleTask
        {
            EntityId = "cdp-canary-deployment-backend",
            Environments = ["infra-dev"]
        };

        await task.ExecuteAsync(
            serviceProvider,
            DateTime.UtcNow.AddMinutes(-10),
            NullLogger<object>.Instance,
            CancellationToken.None);

        await artifactsService.DidNotReceive()
            .FindLatest(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await deploymentExecutor.DidNotReceive()
            .DeployAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_deploy_when_no_artifact_exists()
    {
        var artifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentExecutor = Substitute.For<IServiceDeploymentExecutor>();
        var serviceProvider = CreateProvider(artifactsService, deploymentExecutor);

        artifactsService.FindLatest("cdp-canary-deployment-backend", Arg.Any<CancellationToken>())
            .Returns((DeployableArtifact?)null);

        var task = new MongoDeployServiceScheduleTask
        {
            EntityId = "cdp-canary-deployment-backend",
            Environments = ["infra-dev"]
        };

        await task.ExecuteAsync(
            serviceProvider,
            DateTime.UtcNow,
            NullLogger<object>.Instance,
            CancellationToken.None);

        await deploymentExecutor.DidNotReceive()
            .DeployAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<CancellationToken>());
    }

    private static IServiceProvider CreateProvider(
        IDeployableArtifactsService artifactsService,
        IServiceDeploymentExecutor deploymentExecutor)
    {
        return new ServiceCollection()
            .AddSingleton(artifactsService)
            .AddSingleton(deploymentExecutor)
            .BuildServiceProvider();
    }
}
