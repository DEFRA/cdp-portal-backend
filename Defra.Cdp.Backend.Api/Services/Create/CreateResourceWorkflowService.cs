using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateResourceWorkflowService
{
    Task<ResourceRequestRecord> CreateResources(CreateTenantResourceRequest request, UserDetails? user,
        CancellationToken cancellationToken);
}

public class CreateResourceWorkflowService(
    ITriggerWorkflowService triggerWorkflowService,
    IResourceRequestService resourceRequestService,
    IEntitiesService entitiesService) : ICreateResourceWorkflowService
{
    private const string Repo = "cdp-tenant-config";
    private const string GenericCliWorkflow = "generic-cdp-cli-workflow.yml";

        
    public async Task<ResourceRequestRecord> CreateResources(CreateTenantResourceRequest request, UserDetails? user, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var branch = $"tenant-request-{runId}";
        var title = $"Tenant resource request from {user?.DisplayName ?? "Unknown user"}";
        var inputs = request.ToWorkflowInputs(runId, branch, title);
        
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, GenericCliWorkflow, inputs, cancellationToken);
        
        var names = request.GetServices();

        var entities = await entitiesService.GetEntities(new EntityMatcher { Names = names.ToArray() }, new EntitySearchOptions { Summary = true}, cancellationToken);
        var teams = entities.SelectMany(e => e.Teams).DistinctBy(t=>t.TeamId).ToList();
        
        return await resourceRequestService.RecordRequest(names, teams!, user, request, inputs, response, cancellationToken);
    }
}