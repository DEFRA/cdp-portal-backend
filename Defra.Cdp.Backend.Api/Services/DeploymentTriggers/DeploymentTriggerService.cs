using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public interface IDeploymentTriggerService
{
   public Task<List<DeploymentTrigger>> FindDeploymentTriggers(string repository, CancellationToken cancellationToken);

   public Task<DeploymentTrigger?> FindDeploymentTrigger(string repository, string testSuite, CancellationToken cancellationToken);

}

public class DeploymentTriggerService : MongoService<DeploymentTrigger>, IDeploymentTriggerService
{
   private const string CollectionName = "deploymenttriggers";

   public DeploymentTriggerService(IMongoDbClientFactory connectionFactory,
      ILoggerFactory loggerFactory) : base(connectionFactory, CollectionName, loggerFactory)
   {
   }

   protected override List<CreateIndexModel<DeploymentTrigger>> DefineIndexes(IndexKeysDefinitionBuilder<DeploymentTrigger> builder)
   {
      var repositoryIndex = new CreateIndexModel<DeploymentTrigger>(builder.Ascending(t => t.Repository));
      var testSuiteIndex = new CreateIndexModel<DeploymentTrigger>(builder.Ascending(t => t.TestSuite));

      return [repositoryIndex, testSuiteIndex];
   }

   public async Task<List<DeploymentTrigger>> FindDeploymentTriggers(string repository, CancellationToken cancellationToken)
   {
      return await Collection.Find(t => t.Repository == repository).ToListAsync(cancellationToken);
   }

   public async Task<DeploymentTrigger?> FindDeploymentTrigger(string repository, string testSuite, CancellationToken cancellationToken)
   {
      return await Collection.Find(t => t.Repository == repository && t.TestSuite == testSuite)
         .FirstOrDefaultAsync(cancellationToken);
   }

}
