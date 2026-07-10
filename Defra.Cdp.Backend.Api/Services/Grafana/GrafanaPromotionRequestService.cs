using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPromotionRequestService
{
    Task<PromotionRequestRecord> RecordRequest(
        UserDetails? requestedBy,
        PromotionResource request,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken);
    
}

public class GrafanaPromotionRequestService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<PromotionRequestRecord>(connectionFactory, CollectionName, loggerFactory), IGrafanaPromotionRequestService
{
    private const string CollectionName = "promotionRequests";

    protected override List<CreateIndexModel<PromotionRequestRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<PromotionRequestRecord> builder)
    {
        return
        [
            new CreateIndexModel<PromotionRequestRecord>(builder.Descending(r => r.RequestedAt)),
            new CreateIndexModel<PromotionRequestRecord>(builder.Descending(r => r.ServiceName)),
        ];
    }

    public async Task<PromotionRequestRecord> RecordRequest(
        UserDetails? requestedBy,
        PromotionResource request,
        GitHubTriggerWorkflowResponse? response,
        CancellationToken cancellationToken)
    {
        var record = new PromotionRequestRecord
        {
            ServiceName = request.ServiceName,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Request = request,
            Response = response
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
        return record;
    }
}