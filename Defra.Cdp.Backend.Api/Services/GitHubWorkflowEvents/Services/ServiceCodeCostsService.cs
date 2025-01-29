using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IServiceCodeCostsService : IEventsPersistenceService<ServiceCodeCostsPayload>
{
   public Task<List<ServiceCodeCostsRecord>> FindCosts(string[] environments, string dateFrom, string dateTo, CancellationToken cancellationToken);
}

public class ServiceCodeCostsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<ServiceCodeCostsRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), IServiceCodeCostsService
{
   public const string CollectionName = "servicecodecosts";

   protected override List<CreateIndexModel<ServiceCodeCostsRecord>> DefineIndexes(IndexKeysDefinitionBuilder<ServiceCodeCostsRecord> builder)
   {
      return [];
   }

   public async Task PersistEvent(Event<ServiceCodeCostsPayload> workflowEvent, CancellationToken cancellationToken)
   {
      var logger = loggerFactory.CreateLogger("ServiceCodeCostsService");
      var payload = workflowEvent.Payload;
      var environment = payload.Environment;
      var eventType = workflowEvent.EventType;
      var reportTimestamp = workflowEvent.Timestamp;

      logger.LogInformation("Cost reports for environment {environment} received", environment);

      var records = payload.CostReports.Select(rep =>
        ServiceCodeCostsRecord.FromPayloads(eventType, reportTimestamp, environment, rep)).ToList();

      logger.LogInformation("Cost reports {records} payload", records);

      var bulkOps = records.Select(record => new InsertOneModel<ServiceCodeCostsRecord>(record)).ToList();

      if (bulkOps.Count > 0)
      {
         await Collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
      }

   }

   public async Task<List<ServiceCodeCostsRecord>> FindCosts(string[] environments, string dateFrom, string dateTo, CancellationToken cancellationToken)
   {
      return await Collection.Find(s => environments.Contains(s.Environment)).ToListAsync(cancellationToken);
   }

}
