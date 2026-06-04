using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface IResourceRequestService
{
    Task RecordRequest(
        string entityName,
        UserDetails? requestedBy,
        List<CreateResourceRequest> resources,
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

    public async Task RecordRequest(
        string entityName,
        UserDetails? requestedBy,
        List<CreateResourceRequest> resources,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken)
    {
        var resourcesBson = BsonSerializer.Deserialize<BsonArray>(JsonSerializer.Serialize(resources));
        var workflowBson = workflow != null
            ? BsonDocument.Parse(JsonSerializer.Serialize(workflow))
            : null;

        var record = new ResourceRequestRecord
        {
            EntityName = entityName,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Resources = resourcesBson,
            Workflow = workflowBson
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
    }
}