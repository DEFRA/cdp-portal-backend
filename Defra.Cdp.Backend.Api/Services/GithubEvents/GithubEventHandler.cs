using System.Text.Json;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubEvents;

public interface IGithubEventHandler
{
    Task Handle(GithubEventMessage message, CancellationToken cancellationToken);
}

public class GithubEventHandler(
    ILegacyStatusService legacyStatusService,
    IStatusUpdateService statusUpdateService,
    ITenantServicesService tenantServicesService,
    IDeployableArtifactsService deployableArtifactsService,
    IOptions<GithubOptions> githubConfig,
    ILogger<GithubEventHandler> logger)
    : IGithubEventHandler
{
    public async Task Handle(GithubEventMessage message, CancellationToken cancellationToken)
    {
        var workflowRepo = message.Repository?.Name!;
        var workflowFileName = Path.GetFileName(message.WorkflowRun.Path)!;

        logger.LogInformation($"Handling message of type {message.GithubEvent} for repo {workflowRepo}");

        if (message.WorkflowRun.HeadBranch != "main")
        {
            logger.LogInformation(
                $"Creation handler: Not processing workflow run {workflowRepo}/{workflowFileName}, not running on main branch"
            );
            return;
        }

        if (!ShouldWorkflowBeProcessed(workflowRepo, workflowFileName))
        {
            logger.LogInformation(
                $"Not processing workflow run {workflowRepo}/{workflowFileName}"
            );
            return;
        }

        switch (workflowRepo)
        {
            case { } s when s == githubConfig.Value.Repos.CdpTfSvcInfra:
                await ProcessTfSvcInfraWorkflow(message, workflowFileName, cancellationToken);
                break;
            default:
                await HandleTriggeredWorkflow(message, cancellationToken);
                break;
        }
    }

    private bool ShouldWorkflowBeProcessed(string? workflowRepo, string? workflowFile)
    {
        var reposConfig = githubConfig.Value.Repos;
        var workflowsConfig = githubConfig.Value.Workflows;
        switch (workflowRepo)
        {
            case { } s when s == reposConfig.CdpAppConfig:
                return workflowFile == workflowsConfig.CreateAppConfig;
            case { } s when s == reposConfig.CdpNginxUpstreams:
                return workflowFile == workflowsConfig.CreateNginxUpstreams;
            case { } s when s == reposConfig.CdpSquidProxy:
                return workflowFile == workflowsConfig.CreateSquidConfig;
            case { } s when s == reposConfig.CdpGrafanaSvc:
                return workflowFile == workflowsConfig.CreateDashboard;
            case { } s when s == reposConfig.CdpTfSvcInfra:
                return new[]
                {
                    workflowsConfig.CreateTenantService, workflowsConfig.ApplyTenantService,
                    workflowsConfig.ManualApplyTenantService, workflowsConfig.NotifyPortal
                }.Contains(workflowFile);
            case { } s when s == reposConfig.CdpCreateWorkflows:
                return new[]
                {
                    workflowsConfig.CreateMicroservice, workflowsConfig.CreateRepository,
                    workflowsConfig.CreateJourneyTestSuite, workflowsConfig.CreatePerfTestSuite
                }.Contains(workflowFile);
            default:
                return false;
        }
    }


    private async Task ProcessTfSvcInfraWorkflow(GithubEventMessage message, string workflowFileName,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation($"Handling tf-svc-infra workflow {workflowFileName}");

            var workflowsConfig = githubConfig.Value.Workflows;
            var createTenantServiceWorkflow = workflowsConfig.CreateTenantService;
            var action = message.Action.ToStatus();
            if (workflowFileName == createTenantServiceWorkflow)
            {
                if (action == Status.Completed)
                {
                    logger.LogInformation($"Creation handler: Ignoring {createTenantServiceWorkflow} complete status");
                    return;
                }

                await HandleTriggeredWorkflow(message, ct);
                return;
            }

            if (workflowFileName == workflowsConfig.ApplyTenantService ||
                workflowFileName == workflowsConfig.ManualApplyTenantService ||
                workflowFileName == workflowsConfig.NotifyPortal)
            {
                if (action != Status.Completed) return;

                logger.LogInformation("Event handler: Bulk updating cdp-tf-svc-infra");

                // Any time cdp-tf-svc-infra completes on main, assume all services are successfully created
                var normalisedStatus = StatusHelper.NormaliseStatus(
                    action,
                    message.WorkflowRun.Conclusion?.ToStatus()
                );

                await BulkUpdateTfSvcInfra(
                    message.Repository!.Name!,
                    TrimWorkflowRun(message.WorkflowRun),
                    normalisedStatus,
                    ct
                );

                return;
            }

            logger.LogInformation("Creation handler: Did not process tf-svc-infra workflow");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing tf-svc-infra workflow");
        }
    }

    private async Task BulkUpdateTfSvcInfra(string workflowRepo, TrimmedWorkflowRun trimWorkflowRun, Status status,
        CancellationToken ct)
    {
        var inProgressOrFailed = await legacyStatusService.FindAllInProgressOrFailed(ct);

        List<TenantServiceRecord> servicesToUpdate = [];

        foreach (var service in inProgressOrFailed)
        {
            var tenantService = await tenantServicesService.FindOne(
                new TenantServiceFilter { Environment = "management", Name = service.RepositoryName }, ct);
            if (tenantService != null)
            {
                servicesToUpdate.Add(tenantService);
            }
        }

        logger.LogInformation($"Updating {servicesToUpdate.Count} statuses to {status}");

        foreach (var service in servicesToUpdate)
        {
            logger.LogInformation($"Updating {service.ServiceName} status to {status}");

            await legacyStatusService.UpdateWorkflowStatus(service.ServiceName, workflowRepo, "main", status,
                trimWorkflowRun);

            await statusUpdateService.UpdateOverallStatus(service.ServiceName, ct);

            var runMode = ArtifactRunMode.Service;
            if (service.TestSuite != null)
            {
                runMode = ArtifactRunMode.Job;
            }

            logger.LogInformation(
                $"Creating {runMode} placeholder artifact for {service.ServiceName}");

            await deployableArtifactsService.CreatePlaceholderAsync(service.ServiceName,
                $"https://github.com/{githubConfig.Value.Organisation}/{service.ServiceName}", runMode, ct);
        }
    }

    private static TrimmedWorkflowRun TrimWorkflowRun(WorkflowRun messageWorkflowRun)
    {
        return new TrimmedWorkflowRun(messageWorkflowRun.Name, messageWorkflowRun.Id, messageWorkflowRun.HtmlUrl,
            messageWorkflowRun.CreatedAt, messageWorkflowRun.UpdatedAt, messageWorkflowRun.Path);
    }

    private async Task HandleTriggeredWorkflow(GithubEventMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var workflowRepo = message.Repository?.Name!;
            var headBranch = message.WorkflowRun.HeadBranch!;
            var serviceRepo = message.WorkflowRun.Name!;
            var status = await legacyStatusService.StatusForRepositoryName(serviceRepo, cancellationToken);

            if (status == null)
            {
                logger.LogInformation($"No status found for {serviceRepo}, unable to process event");
                return;
            }

            var normalisedStatus = StatusHelper.NormaliseStatus(
                message.Action.ToStatus(),
                message.WorkflowRun.Conclusion?.ToStatus()
            );

            logger.LogInformation(
                $"Attempting to update {workflowRepo} status for {serviceRepo} to {normalisedStatus}");

            await legacyStatusService.UpdateWorkflowStatus(
                serviceRepo,
                workflowRepo,
                headBranch,
                normalisedStatus,
                TrimWorkflowRun(message.WorkflowRun)
            );

            await statusUpdateService.UpdateOverallStatus(serviceRepo, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling the triggered workflow.");
        }
    }
}

public record TrimmedWorkflowRun(
    string? name,
    long? id,
    [property: BsonElement("html_url")] string? htmlUrl,
    [property: BsonElement("created_at")] DateTime? createdAt,
    [property: BsonElement("updated_at")] DateTime? updatedAt,
    string? path);