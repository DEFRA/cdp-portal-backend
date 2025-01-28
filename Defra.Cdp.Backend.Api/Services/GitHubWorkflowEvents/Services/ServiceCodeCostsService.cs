using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IServiceCodeCostsService : IEventsPersistenceService<ServiceCodeCostsPayload>
{
   public Task<List<ServiceCodeCostsRecord>> FindCosts(string environment, CancellationToken cancellationToken);
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
      var env = workflowEvent.Payload.Environment;


   }

   public async Task<List<ServiceCodeCostsRecord>> FindCosts(string environment, CancellationToken cancellationToken)
   {
      return await Collection.Find(s => s.Environment == environment).ToListAsync(cancellationToken);
   }

}

[BsonIgnoreExtraElements]
public record ServiceCodeCostsRecord(string Environment, List<ServiceCodeCostReports> costReports)
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;
}

[BsonIgnoreExtraElements]
public record ServiceCodeCostReports(string ServiceCode, string AwsServiceName, CostReport costReport)
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;
}
