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
        DashboardPromotionRequest request,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken);

    Task<PromotionRequestRecord> RecordRequest(
        UserDetails? requestedBy,
        AlertPromotionRequest request,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken);
    
    Task<List<PromotionRequestRecord>> GetLatestRequestsForService(string name, CancellationToken cancellationToken);

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
        DashboardPromotionRequest request,
        GitHubTriggerWorkflowResponse? response,
        CancellationToken cancellationToken)
    {
        var record = new PromotionRequestRecord
        {
            ServiceName = request.ServiceName,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Dashboard = request,
            Response = response
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
        return record;
    }

    public async Task<PromotionRequestRecord> RecordRequest(
        UserDetails? requestedBy,
        AlertPromotionRequest request,
        GitHubTriggerWorkflowResponse? workflow,
        CancellationToken cancellationToken)
    {
        var record = new PromotionRequestRecord
        {
            ServiceName = request.ServiceName,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Alert = request,
            Response = workflow
        };

        await Collection.InsertOneAsync(record, cancellationToken: cancellationToken);
        return record;
    }

    
    /// <summary>
    /// For each dashboard in the playground returns the most recent promotion request record.
    /// Also returns the most recent alert promotion, if it exists.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<PromotionRequestRecord>> GetLatestRequestsForService(string name, CancellationToken cancellationToken)
    {
        var dashboardsPromotions = await Collection.Aggregate()
            .Match(pr => pr.ServiceName == name && pr.Dashboard != null)
            .SortByDescending(pr => pr.RequestedAt)
            .Group(pr => pr.Dashboard!.DashboardUid, g => g.First())
            .ToListAsync(cancellationToken);

        var alertPromotion = await Collection.Find(pr => pr.ServiceName == name && pr.Alert != null)
            .SortByDescending(pr => pr.RequestedAt).FirstOrDefaultAsync(cancellationToken);

        if (alertPromotion != null)
        {
            dashboardsPromotions.Add(alertPromotion);
        }

        return dashboardsPromotions;
    }
}