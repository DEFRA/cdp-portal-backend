using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITotalCostsService : IEventsPersistenceService<TotalCostsPayload>
{
   public Task<TotalCosts> FindAllCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
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

   public async Task PersistEvent(Event<TotalCostsPayload> workflowEvent, CancellationToken cancellationToken)
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

   public async Task<TotalCosts> FindAllCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken)
   {
      var eventType = timeUnit switch
      {
         ReportTimeUnit.Monthly => "last-calendar-month-total-cost",
         ReportTimeUnit.ThirtyDays => "last-30-days-total-cost",
         ReportTimeUnit.Daily => "last-calendar-day-total-cost",
         _ => throw new ArgumentOutOfRangeException(nameof(timeUnit), timeUnit, null)
      };
      var filter = Builders<TotalCostsRecord>.Filter.Gte(r => r.CostReport.DateFrom, dateFrom) &
                   Builders<TotalCostsRecord>.Filter.Lte(r => r.CostReport.DateTo, dateTo) &
                   Builders<TotalCostsRecord>.Filter.Eq(r => r.EventType, eventType);
      var sorting = Builders<TotalCostsRecord>.Sort.Descending(r => r.EventTimestamp).Ascending(r => r.Environment);
      var costs = await Collection.Find(filter).Sort(sorting).ToListAsync(cancellationToken);
      var trimmedCosts = await onlyLatestReports(costs, cancellationToken);
      return new TotalCosts(timeUnit, dateFrom, dateTo, trimmedCosts);
   }

   private async Task<List<TotalCostsRecord>> onlyLatestReports(List<TotalCostsRecord> costsRecords, CancellationToken cancellationToken)
   {
      var environments = costsRecords.GroupBy(r => r.Environment).ToDictionary(g => g.Key, g => g.ToList());
      var latestRecords = new List<TotalCostsRecord>();
      foreach (var environment in environments.Keys)
      {
         var dateFroms = environments[environment].GroupBy(r => r.CostReport.DateFrom).ToDictionary(g => g.Key, g => g.ToList());
         foreach (var dateFrom in dateFroms.Keys)
         {
            _logger.LogInformation("Finding latest record for environment {environment} and dateFrom {dateFrom}", environment, dateFrom);
            foreach (var record in dateFroms[dateFrom])
            {
               _logger.LogInformation("Record: {record}", record);
            }
            var latestRecord = dateFroms[dateFrom].OrderByDescending(r => r.EventTimestamp).OrderByDescending(r => r.CreatedAt).First();
            latestRecords.Add(latestRecord);
         }
      }
      return latestRecords;
   }

}
