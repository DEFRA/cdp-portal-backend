using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public interface IDeploymentTriggerService
{
    public Task<List<DeploymentTrigger>> FindTriggersForDeployment(Deployment deployment,
        CancellationToken cancellationToken);
}

public class DeploymentTriggerService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<DeploymentTrigger>(connectionFactory, CollectionName, loggerFactory), IDeploymentTriggerService
{
    private const string CollectionName = "deploymenttriggers";

    protected override List<CreateIndexModel<DeploymentTrigger>> DefineIndexes(
        IndexKeysDefinitionBuilder<DeploymentTrigger> builder)
    {
        var repositoryIndex = new CreateIndexModel<DeploymentTrigger>(builder.Ascending(t => t.Repository));
        var testSuiteIndex = new CreateIndexModel<DeploymentTrigger>(builder.Ascending(t => t.TestSuite));

        return [repositoryIndex, testSuiteIndex];
    }

    public async Task<List<DeploymentTrigger>> FindTriggersForDeployment(Deployment deployment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(t =>
                t.Repository == deployment.Service && t.Environments.Contains(deployment.Environment))
            .ToListAsync(cancellationToken);
    }
}