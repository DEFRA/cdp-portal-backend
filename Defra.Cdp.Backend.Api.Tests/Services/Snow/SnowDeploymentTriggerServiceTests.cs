using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Snow;
using Defra.Cdp.Backend.Api.Services.Snow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using EntityStatus = Defra.Cdp.Backend.Api.Services.Entities.Model.Status;
using EntityType = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Tests.Services.Snow;

public class SnowDeploymentTriggerServiceTests
{
    [Fact]
    public async Task TriggersWorkflowWithStringifiedPayloads()
    {
        var triggerWorkflowService = Substitute.For<ITriggerWorkflowService>();
        var entitiesService = Substitute.For<IEntitiesService>();

        entitiesService.GetEntity("cdp-portal-backend", Arg.Any<CancellationToken>())
            .Returns(new Entity
            {
                Name = "cdp-portal-backend",
                Type = EntityType.Microservice,
                Status = EntityStatus.Created,
                Teams =
                [
                    new Team { TeamId = "zebra", Name = "Zebra Team" },
                    new Team { TeamId = "alpha", Name = "Alpha Team" }
                ]
            });
        triggerWorkflowService
            .TriggerWorkflow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SnowDeploymentWorkflowInputs>(),
                Arg.Any<CancellationToken>())
            .Returns(new GitHubTriggerWorkflowResponse { WorkflowRunId = 12345 });

        var service = new SnowDeploymentTriggerService(triggerWorkflowService, entitiesService,
            NullLogger<SnowDeploymentTriggerService>.Instance);
        var statusChange = new ServiceStatusChange
        {
            DeploymentId = "deployment-123",
            Environment = "prod",
            OldStatus = "SERVICE_DEPLOYMENT_IN_PROGRESS",
            NewStatus = "SERVICE_DEPLOYMENT_COMPLETED",
            EntityId = "cdp-portal-backend",
            Version = "1.2.3",
            PreviousVersion = "1.2.2",
            UserDisplayName = "Portal User"
        };

        await service.TriggerDeploymentRecord(statusChange, TestContext.Current.CancellationToken);

        await triggerWorkflowService.Received(1).TriggerWorkflow(
            "cdp-deployments-snow",
            "deploy.yml",
            Arg.Any<SnowDeploymentWorkflowInputs>(),
            Arg.Any<CancellationToken>());

        var call = triggerWorkflowService.ReceivedCalls().Single();
        var inputs = Assert.IsType<SnowDeploymentWorkflowInputs>(call.GetArguments()[2]);
        Assert.NotNull(inputs.ExtendedPayload);
        var portalPayload = JsonSerializer.Deserialize<SnowPortalPayload>(inputs.PortalPayload);
        var extendedPayload = JsonSerializer.Deserialize<SnowExtendedPayload>(inputs.ExtendedPayload!);

        Assert.NotNull(portalPayload);
        Assert.NotNull(extendedPayload);
        Assert.Equal("cdp-portal-backend", portalPayload.Service);
        Assert.Equal("deployment-123", portalPayload.CdpDeploymentId);
        Assert.Equal("SERVICE_DEPLOYMENT_COMPLETED", portalPayload.LastDeploymentStatus);
        Assert.NotNull(portalPayload.User);
        Assert.Equal("Portal User", portalPayload.User.DisplayName);
        Assert.Equal("Alpha Team", extendedPayload.TeamName);
        Assert.Equal("1.2.2", extendedPayload.PreviousVersion);
    }

    [Fact]
    public async Task UsesUnknownTeamNameWhenServiceHasNoTeams()
    {
        var triggerWorkflowService = Substitute.For<ITriggerWorkflowService>();
        var entitiesService = Substitute.For<IEntitiesService>();

        entitiesService.GetEntity("cdp-portal-backend", Arg.Any<CancellationToken>())
            .Returns(new Entity
            {
                Name = "cdp-portal-backend",
                Type = EntityType.Microservice,
                Status = EntityStatus.Created
            });
        triggerWorkflowService
            .TriggerWorkflow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SnowDeploymentWorkflowInputs>(),
                Arg.Any<CancellationToken>())
            .Returns(new GitHubTriggerWorkflowResponse());

        var service = new SnowDeploymentTriggerService(triggerWorkflowService, entitiesService,
            NullLogger<SnowDeploymentTriggerService>.Instance);
        var statusChange = new ServiceStatusChange
        {
            DeploymentId = "deployment-123",
            Environment = "prod",
            OldStatus = "SERVICE_DEPLOYMENT_IN_PROGRESS",
            NewStatus = "SERVICE_DEPLOYMENT_COMPLETED",
            EntityId = "cdp-portal-backend",
            Version = "1.2.3",
            PreviousVersion = null,
            UserDisplayName = null
        };

        await service.TriggerDeploymentRecord(statusChange, TestContext.Current.CancellationToken);

        var call = triggerWorkflowService.ReceivedCalls().Single();
        var inputs = Assert.IsType<SnowDeploymentWorkflowInputs>(call.GetArguments()[2]);
        Assert.NotNull(inputs.ExtendedPayload);
        var extendedPayload = JsonSerializer.Deserialize<SnowExtendedPayload>(inputs.ExtendedPayload!);
        var portalPayload = JsonSerializer.Deserialize<SnowPortalPayload>(inputs.PortalPayload);

        Assert.NotNull(extendedPayload);
        Assert.NotNull(portalPayload);
        Assert.Equal(SnowPayloadDefaults.Unknown, extendedPayload.TeamName);
        Assert.Null(portalPayload.User);
    }
}
