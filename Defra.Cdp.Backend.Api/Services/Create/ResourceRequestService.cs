using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface IResourceRequestService
{
    Task<ResourceRequestRecord> RecordRequest(
        string entityName,
        UserDetails? requestedBy,
        CreateTenantResourceRequest resources,
        GenericCdpWorkflowInputs inputs,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken);

    Task<bool> AttachPullRequest(string runId, ResourceRequestPullRequest pullRequest,
        CancellationToken cancellationToken);

    Task<ResourceRequestRecord?> GetByEntityAndId(string entityName, ObjectId id, CancellationToken cancellationToken);
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
                new CreateIndexOptions { Sparse = true, Unique = true })
        ];
    }
    
    public async Task<ResourceRequestRecord> RecordRequest(
        string entityName,
        UserDetails? requestedBy,
        CreateTenantResourceRequest resources,
        GenericCdpWorkflowInputs inputs,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken)
    {
        var record = new ResourceRequestRecord
        {
            EntityName = entityName,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Resources = resources,
            Inputs = inputs,
            Workflow = workflow
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
        return record;
    }

    public async Task<bool> AttachPullRequest(string runId, ResourceRequestPullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ResourceRequestRecord>.Filter.Where(record =>
            record.Inputs != null &&
            record.Inputs.RunId == runId);

        var update = Builders<ResourceRequestRecord>.Update.Set(record => record.PullRequest, pullRequest);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<ResourceRequestRecord?> GetByEntityAndId(string entityName, ObjectId id,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ResourceRequestRecord>.Filter.Where(record =>
            record.EntityName == entityName &&
            record.Id == id);
        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }
}