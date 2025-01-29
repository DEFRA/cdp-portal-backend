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
   public Task<List<TotalCostsRecord>> FindCosts(string environment, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
}

public class TotalCostsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<TotalCostsRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), ITotalCostsService
{
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
      var reportTimestamp = workflowEvent.Timestamp;
      var payload = workflowEvent.Payload;
      var environment = payload.Environment;
      var costReport = payload.CostReports;

      logger.LogInformation("Cost reports for environment {environment} received", environment);

      var record = TotalCostsRecord.FromPayloads(eventType, reportTimestamp, environment, costReport);

      logger.LogInformation("Cost reports {record} payload", record);

      await Collection.InsertOneAsync(record, null, cancellationToken);

   }

   public async Task<List<TotalCostsRecord>> FindCosts(string environment, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken)
   {
      return await Collection.Find(s => s.Environment == environment).ToListAsync(cancellationToken);
   }

}
