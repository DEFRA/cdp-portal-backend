using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws.AutoDeploymentTriggers;

public interface IAutoDeploymentTriggerService
{
   public Task<AutoDeploymentTrigger?> FindForServiceName(string serviceName, CancellationToken cancellationToken);
   
   public Task PersistTrigger(AutoDeploymentTrigger autoDeploymentTrigger, CancellationToken cancellationToken);
}

public class AutoDeploymentTriggerService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<AutoDeploymentTrigger>(connectionFactory, CollectionName, loggerFactory),
        IAutoDeploymentTriggerService
{
    private readonly ILogger<AutoDeploymentTriggerService> _logger = loggerFactory.CreateLogger<AutoDeploymentTriggerService>();
    
   private const string CollectionName = "autodeploymenttriggers";

   protected override List<CreateIndexModel<AutoDeploymentTrigger>> DefineIndexes(IndexKeysDefinitionBuilder<AutoDeploymentTrigger> builder)
   {
      var repositoryIndex = new CreateIndexModel<AutoDeploymentTrigger>(builder.Ascending(t => t.ServiceName));

      return [repositoryIndex];
   }

   public async Task<AutoDeploymentTrigger?> FindForServiceName(string serviceName, CancellationToken cancellationToken)
   {
      return await Collection.Find(t => t.ServiceName == serviceName)
         .SingleOrDefaultAsync(cancellationToken);
   }

   public async Task PersistTrigger(AutoDeploymentTrigger autoDeploymentTrigger, CancellationToken cancellationToken)
   {
       _logger.LogInformation("Persisting auto deployment trigger for service: {Service}", autoDeploymentTrigger.ServiceName);

       var triggerInDb = await FindForServiceName(autoDeploymentTrigger.ServiceName, cancellationToken);
    
       if (triggerInDb != null)
       {
           var filter = Builders<AutoDeploymentTrigger>.Filter.Eq("_id", triggerInDb.Id);
           await Collection.DeleteManyAsync(filter, cancellationToken);
       }

       if (autoDeploymentTrigger.Environments.Count > 0)
       {
           await Collection.InsertOneAsync(autoDeploymentTrigger, cancellationToken: cancellationToken);
       }
   }
}