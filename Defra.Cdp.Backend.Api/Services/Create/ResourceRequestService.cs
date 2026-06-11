using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
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
}

public class ResourceRequestService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<ResourceRequestRecord>(connectionFactory, CollectionName, loggerFactory), IResourceRequestService
{
    public const string CollectionName = "resourceRequests";

    protected override List<CreateIndexModel<ResourceRequestRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ResourceRequestRecord> builder)
    {
        return [new CreateIndexModel<ResourceRequestRecord>(builder.Descending(r => r.RequestedAt))];
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
}