using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Deployments;

public class ServiceDeploymentExecutorTests
{
    private readonly IDeploymentsService _deploymentsService = Substitute.For<IDeploymentsService>();
    private readonly ISelfServiceOpsClient _selfServiceOpsClient = Substitute.For<ISelfServiceOpsClient>();
    private readonly IAppConfigVersionsService _appConfigVersionsService = Substitute.For<IAppConfigVersionsService>();
    private readonly UserDetails _user = new() { Id = "user-1", DisplayName = "Scheduler" };

    [Fact]
    public async Task Should_skip_when_deployment_settings_are_missing()
    {
        var sut = CreateSut();

        _deploymentsService.FindDeploymentSettings("service-a", "dev", Arg.Any<CancellationToken>())
            .Returns((DeploymentSettings?)null);

        await sut.DeployAsync("service-a", "1.2.3", "dev", _user, CancellationToken.None);

        await _appConfigVersionsService.DidNotReceive()
            .FindLatestAppConfigVersion(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _selfServiceOpsClient.DidNotReceive()
            .AutoDeployService(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<DeploymentSettings>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_skip_when_config_version_is_missing()
    {
        var sut = CreateSut();
        var settings = new DeploymentSettings { Cpu = "1024", Memory = "2048", InstanceCount = 1 };

        _deploymentsService.FindDeploymentSettings("service-a", "dev", Arg.Any<CancellationToken>())
            .Returns(settings);
        _appConfigVersionsService.FindLatestAppConfigVersion("dev", Arg.Any<CancellationToken>())
            .Returns((AppConfigVersion?)null);

        await sut.DeployAsync("service-a", "1.2.3", "dev", _user, CancellationToken.None);

        await _selfServiceOpsClient.DidNotReceive()
            .AutoDeployService(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<DeploymentSettings>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_deploy_when_settings_and_config_exist()
    {
        var sut = CreateSut();
        var settings = new DeploymentSettings { Cpu = "1024", Memory = "2048", InstanceCount = 1 };
        var configVersion = new AppConfigVersion("abc123", DateTime.UtcNow, "dev");

        _deploymentsService.FindDeploymentSettings("service-a", "dev", Arg.Any<CancellationToken>())
            .Returns(settings);
        _appConfigVersionsService.FindLatestAppConfigVersion("dev", Arg.Any<CancellationToken>())
            .Returns(configVersion);

        await sut.DeployAsync("service-a", "1.2.3", "dev", _user, CancellationToken.None);

        await _selfServiceOpsClient.Received(1)
            .AutoDeployService(
                "service-a",
                "1.2.3",
                "dev",
                _user,
                settings,
                "abc123",
                Arg.Any<CancellationToken>());
    }

    private ServiceDeploymentExecutor CreateSut()
    {
        return new ServiceDeploymentExecutor(
            _deploymentsService,
            _selfServiceOpsClient,
            _appConfigVersionsService,
            NullLogger<ServiceDeploymentExecutor>.Instance);
    }
}
