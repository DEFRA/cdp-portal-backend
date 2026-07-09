using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public static class PrStatus
{
    public const string Pending = "pending";
    public const string Requested = "requested";
    public const string Merged = "merged";
    public const string Closed = "closed";

}

public interface IResourceRequestService
{
    Task<ResourceRequestRecord> RecordRequest(
        List<string> entityNames,
        UserDetails? requestedBy,
        CreateTenantResourceRequest resources,
        GenericCdpWorkflowInputs inputs,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken);

    Task<ResourceRequestRecord?> AttachPullRequest(string runId, ResourceRequestPullRequest pullRequest,
        CancellationToken cancellationToken);
    
    Task<ResourceRequestRecord?> UpdatePullRequestStatus(int prNumber, string status,
        CancellationToken cancellationToken);

    Task<ResourceRequestRecord?> FindByWorkflowId(long workflowRunId, CancellationToken cancellationToken = default);
    
    Task<List<ResourceRequestRecord>> Find(ResourceRequestMatcher matcher, CancellationToken cancellationToken = default);
}


public class ResourceRequestService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<ResourceRequestRecord>(connectionFactory, CollectionName, loggerFactory), IResourceRequestService
{
    public const string CollectionName = "resourceRequests";

    protected override List<CreateIndexModel<ResourceRequestRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ResourceRequestRecord> builder)
    {
        return
        [
            new CreateIndexModel<ResourceRequestRecord>(builder.Descending(r => r.RequestedAt)),
            new CreateIndexModel<ResourceRequestRecord>(
                builder.Ascending(r => r.Inputs!.RunId),
                new CreateIndexOptions { Sparse = true, Unique = true }),
            new CreateIndexModel<ResourceRequestRecord>(
                builder.Ascending(r => r.Workflow!.WorkflowRunId),
                new CreateIndexOptions { Sparse = true, Unique = true }),
            new CreateIndexModel<ResourceRequestRecord>(builder.Descending(r => r.PullRequest!.Number))];
    }

    public async Task<ResourceRequestRecord> RecordRequest(
        List<string> entityNames,
        UserDetails? requestedBy,
        CreateTenantResourceRequest resources,
        GenericCdpWorkflowInputs inputs,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken)
    {
        var record = new ResourceRequestRecord
        {
            Status = PrStatus.Pending,
            EntityName = entityNames.FirstOrDefault(""),
            Entities = entityNames,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Resources = resources,
            Inputs = inputs,
            Workflow = workflow
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
        return record;
    }

    public async Task<ResourceRequestRecord?> AttachPullRequest(string runId, ResourceRequestPullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ResourceRequestRecord>.Filter.Where(record =>
            record.Inputs != null &&
            record.Inputs.RunId == runId);

        var update = Builders<ResourceRequestRecord>.Update
            .Set(record => record.PullRequest, pullRequest)
            .Set(record => record.Status, PrStatus.Requested);
        
        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ResourceRequestRecord> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }
    
    public async Task<ResourceRequestRecord?> UpdatePullRequestStatus(int prNumber, string status,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ResourceRequestRecord>.Filter.Eq(record => record.PullRequest!.Number, prNumber);
        var update = Builders<ResourceRequestRecord>.Update.Set(record => record.Status, status);
        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ResourceRequestRecord> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<ResourceRequestRecord?> FindByWorkflowId(long workflowRunId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(record => record.Workflow != null && record.Workflow.WorkflowRunId == workflowRunId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ResourceRequestRecord>> Find(ResourceRequestMatcher matcher, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(matcher.Match()).ToListAsync(cancellationToken);
    }
}