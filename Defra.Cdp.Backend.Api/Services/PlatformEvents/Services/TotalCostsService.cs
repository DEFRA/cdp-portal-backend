using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.PlatformEvents.Services;

public interface ITotalCostsService : IEventsPersistenceService<TotalCostsPayload>
{
   public Task<TotalCosts> FindCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
}

public class TotalCostsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<TotalCostsRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), ITotalCostsService
{
   private ILogger _logger = loggerFactory.CreateLogger("TotalCostsService");
   public const string CollectionName = "totalcosts";

   protected override List<CreateIndexModel<TotalCostsRecord>> DefineIndexes(IndexKeysDefinitionBuilder<TotalCostsRecord> builder)
   {
      return [];
   }

   public async Task PersistEvent(CommonEvent<TotalCostsPayload> workflowEvent, CancellationToken cancellationToken)
   {
      var env = workflowEvent.Payload.Environment;

      var logger = loggerFactory.CreateLogger("ServiceCodeCostsService");
      var eventType = workflowEvent.EventType;
      var eventTimestamp = workflowEvent.Timestamp;
      var payload = workflowEvent.Payload;
      var environment = payload.Environment;
      var costReport = payload.CostReports;

      logger.LogInformation("Total cost reports for eventType {eventType} received", eventType);

      var record = TotalCostsRecord.FromPayloads(eventType, eventTimestamp, environment, costReport);

      await Collection.InsertOneAsync(record, null, cancellationToken);

   }

   public async Task<TotalCosts> FindCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken)
   {
      var eventType = timeUnit switch
      {
         ReportTimeUnit.Monthly => "last-calendar-month-total-cost",
         ReportTimeUnit.ThirtyDays => "last-30-days-total-cost",
         ReportTimeUnit.Daily => "last-calendar-day-total-cost",
         _ => throw new ArgumentOutOfRangeException(nameof(timeUnit), timeUnit, null)
      };
      var builder = Builders<TotalCostsRecord>.Filter;
      var filter = builder.Gte(r => r.CostReport.DateFrom, dateFrom) &
                   builder.Lte(r => r.CostReport.DateTo, dateTo) &
                   builder.Eq(r => r.EventType, eventType);
      var sorting = Builders<TotalCostsRecord>.Sort.Descending(r => r.EventTimestamp).Ascending(r => r.Environment);
      var costs = await Collection.Find(filter).Sort(sorting).ToListAsync(cancellationToken);
      var trimmedCosts = onlyLatestReports(costs);
      return new TotalCosts(timeUnit, dateFrom, dateTo, trimmedCosts);
   }

   private List<TotalCostsRecord> onlyLatestReports(List<TotalCostsRecord> costsRecords)
   {
      return costsRecords.GroupBy(r => r.Environment)
                         .SelectMany(r => r.GroupBy(r => r.CostReport.DateFrom))
                         .Select(r => r.OrderByDescending(r => r.EventTimestamp)
                                       .OrderByDescending(r => r.CreatedAt)
                                       .First())
                         .ToList();
   }

}
