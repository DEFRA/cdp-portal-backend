using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IEnvironmentCostsService : IEventsPersistenceService<EnvironmentCostsPayload>
{
   public Task<List<EnvironmentCostsRecord>> FindCosts(string environment, CancellationToken cancellationToken);
}

public class EnvironmentCostsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : MongoService<EnvironmentCostsRecord>(
    connectionFactory,
    CollectionName,
    loggerFactory), IEnvironmentCostsService
{
   public const string CollectionName = "environmentcosts";

   protected override List<CreateIndexModel<EnvironmentCostsRecord>> DefineIndexes(IndexKeysDefinitionBuilder<EnvironmentCostsRecord> builder)
   {
      return [];
   }

   public async Task PersistEvent(Event<EnvironmentCostsPayload> workflowEvent, CancellationToken cancellationToken)
   {
      var env = workflowEvent.Payload.Environment;


   }

   public async Task<List<EnvironmentCostsRecord>> FindCosts(string environment, CancellationToken cancellationToken)
   {
      return await Collection.Find(s => s.Environment == environment).ToListAsync(cancellationToken);
   }

}

[BsonIgnoreExtraElements]
public record EnvironmentCostsRecord(string Environment, CostReport costReports)
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;
}


public record CostReport(decimal Cost, string Unit, DateOnly DateFrom, DateOnly DateTo)
{
   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;
}
