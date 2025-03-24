using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;

public interface IAutoTestRunTriggerService
{
    public Task<AutoTestRunTrigger?> FindForService(string service, CancellationToken cancellationToken);

    public Task<AutoTestRunTrigger?> PersistTrigger(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken);
}

public class AutoTestRunTriggerService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<AutoTestRunTrigger>(connectionFactory, CollectionName, loggerFactory), IAutoTestRunTriggerService
{
    private readonly ILogger<AutoTestRunTriggerService> _logger =
        loggerFactory.CreateLogger<AutoTestRunTriggerService>();

    private const string CollectionName = "autotestruntriggers";

    protected override List<CreateIndexModel<AutoTestRunTrigger>> DefineIndexes(
        IndexKeysDefinitionBuilder<AutoTestRunTrigger> builder)
    {
        var repositoryIndex = new CreateIndexModel<AutoTestRunTrigger>(builder.Ascending(t => t.ServiceName));

        return [repositoryIndex];
    }

    public async Task<AutoTestRunTrigger?> FindForService(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.ServiceName == service)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> PersistTrigger(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persisting auto test-run trigger for service: {Service}",
            autoTestRunTrigger.ServiceName);

        //We don't want to allow auto-test-run on prod
        autoTestRunTrigger.EnvironmentTestSuitesMap.Remove("prod");

        foreach (var env in autoTestRunTrigger.EnvironmentTestSuitesMap.Keys.Where(env =>
                     autoTestRunTrigger.EnvironmentTestSuitesMap[env]?.Count == 0))
        {
            autoTestRunTrigger.EnvironmentTestSuitesMap.Remove(env);
        }

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        if (triggerInDb != null)
        {
            var filter = Builders<AutoTestRunTrigger>.Filter.Eq("_id", triggerInDb.Id);
            await Collection.DeleteManyAsync(filter, cancellationToken);
        }

        if (autoTestRunTrigger.EnvironmentTestSuitesMap.Count > 0)
        {
            await Collection.InsertOneAsync(autoTestRunTrigger, cancellationToken: cancellationToken);
        }

        return await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
    }
}