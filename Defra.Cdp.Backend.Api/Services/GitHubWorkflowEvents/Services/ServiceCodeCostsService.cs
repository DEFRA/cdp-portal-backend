using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using Microsoft.VisualBasic;
using Defra.Cdp.Backend.Api.Endpoints;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IServiceCodeCostsService : IEventsPersistenceService<ServiceCodeCostsPayload>
{

   public Task<ServiceCodesCosts> FindCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
}

public class ServiceCodeCostsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<ServiceCodeCostsRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), IServiceCodeCostsService
{
   private ILogger _logger = loggerFactory.CreateLogger("ServiceCodeCostsService");
   public const string CollectionName = "servicecodecosts";

   protected override List<CreateIndexModel<ServiceCodeCostsRecord>> DefineIndexes(IndexKeysDefinitionBuilder<ServiceCodeCostsRecord> builder)
   {
      return [];
   }

   public async Task PersistEvent(Event<ServiceCodeCostsPayload> workflowEvent, CancellationToken cancellationToken)
   {
      var logger = loggerFactory.CreateLogger("ServiceCodeCostsService");
      var payload = workflowEvent.Payload;
      var eventType = workflowEvent.EventType;
      var eventTimestamp = workflowEvent.Timestamp;
      var environment = payload.Environment;

      logger.LogInformation("Service code cost reports for eventType {eventType} received", eventType);

      var records = payload.CostReports.Select(rep =>
        ServiceCodeCostsRecord.FromPayloads(eventType, eventTimestamp, environment, rep)).ToList();

      var bulkOps = records.Select(record => new InsertOneModel<ServiceCodeCostsRecord>(record)).ToList();

      if (bulkOps.Count > 0)
      {
         await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
      }

   }

   public async Task<ServiceCodesCosts> FindCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken)
   {
      var eventType = timeUnit switch
      {
         ReportTimeUnit.Monthly => "last-calendar-month-costs-by-service-code",
         ReportTimeUnit.ThirtyDays => "last-30-days-costs-by-service-code",
         ReportTimeUnit.Daily => "last-calendar-day-costs-by-service-code",
         _ => throw new ArgumentOutOfRangeException(nameof(timeUnit), timeUnit, null)
      };
      var builder = Builders<ServiceCodeCostsRecord>.Filter;
      var filter = builder.Gte(r => r.CostReport.DateFrom, dateFrom) &
                   builder.Lte(r => r.CostReport.DateTo, dateTo) &
                   builder.Eq(r => r.EventType, eventType);
      var sorting = Builders<ServiceCodeCostsRecord>.Sort.Descending(r => r.EventTimestamp).Ascending(r => r.ServiceCode).Ascending(r => r.Environment);
      var costs = await Collection.Find(filter).Sort(sorting).ToListAsync(cancellationToken);
      var trimmedCosts = onlyLatestReports(costs);
      return new ServiceCodesCosts(timeUnit, dateFrom, dateTo, trimmedCosts);
   }

   private List<ServiceCodeCostsRecord> onlyLatestReports(List<ServiceCodeCostsRecord> costsRecords)
   {
      return costsRecords.GroupBy(r => r.Environment)
                         .SelectMany(r => r.GroupBy(r => r.ServiceCode))
                         .SelectMany(r => r.GroupBy(r => r.AwsService))
                         .SelectMany(r => r.GroupBy(r => r.CostReport.DateFrom))
                         .Select(r => r.OrderByDescending(r => r.EventTimestamp)
                                       .OrderByDescending(r => r.CreatedAt)
                                       .First())
                         .ToList();
   }

}
