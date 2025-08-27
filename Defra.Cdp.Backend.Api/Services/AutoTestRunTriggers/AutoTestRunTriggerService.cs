using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;

public interface IAutoTestRunTriggerService
{
    public Task<AutoTestRunTrigger?> FindForService(string serviceName, CancellationToken cancellationToken);

    public Task<AutoTestRunTrigger?> SaveTrigger(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken);

    public Task<AutoTestRunTrigger?> RemoveTestRun(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken);

    public Task<AutoTestRunTrigger?> UpdateTestRun(AutoTestRunTrigger autoTestRunTrigger,
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

    public async Task<AutoTestRunTrigger?> FindForService(string serviceName,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.ServiceName == serviceName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> SaveTrigger(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating auto test-run trigger for service: {Service}",
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        if (triggerInDb != null)
        {
            // Update existing trigger
            if (autoTestRunTrigger.Environments.Count == 0)
                triggerInDb.TestSuites.Remove(autoTestRunTrigger.TestSuite);
            else
                triggerInDb.TestSuites[autoTestRunTrigger.TestSuite] = autoTestRunTrigger.Environments;

            var filter = Builders<AutoTestRunTrigger>.Filter.Eq(t => t.ServiceName, autoTestRunTrigger.ServiceName);
            var update = Builders<AutoTestRunTrigger>.Update.Set(t => t.TestSuites, triggerInDb.TestSuites);

            await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
        else
        {
            // Create new trigger
            var newTrigger = new AutoTestRunTrigger
            {
                ServiceName = autoTestRunTrigger.ServiceName,
                TestSuites = new Dictionary<string, List<string>>
                {
                    { autoTestRunTrigger.TestSuite, autoTestRunTrigger.Environments }
                }
            };

            await Collection.InsertOneAsync(newTrigger, cancellationToken: cancellationToken);
        }

        return await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> RemoveTestRun(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing test run {TestSuite} for service: {Service}", autoTestRunTrigger.TestSuite,
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        if (triggerInDb == null) return null;

        triggerInDb.TestSuites.Remove(autoTestRunTrigger.TestSuite);

        var filter = Builders<AutoTestRunTrigger>.Filter.Eq(t => t.ServiceName, autoTestRunTrigger.ServiceName);
        var update = Builders<AutoTestRunTrigger>.Update.Set(t => t.TestSuites, triggerInDb.TestSuites);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

        return await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> UpdateTestRun(AutoTestRunTrigger autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating test run {TestSuite} for service: {Service}", autoTestRunTrigger.TestSuite,
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        if (triggerInDb == null) return null;

        // Update existing trigger
        if (autoTestRunTrigger.Environments.Count == 0)
            triggerInDb.TestSuites.Remove(autoTestRunTrigger.TestSuite);
        else
            triggerInDb.TestSuites[autoTestRunTrigger.TestSuite] = autoTestRunTrigger.Environments;

        var filter = Builders<AutoTestRunTrigger>.Filter.Eq(t => t.ServiceName, autoTestRunTrigger.ServiceName);
        var update = Builders<AutoTestRunTrigger>.Update.Set(t => t.TestSuites, triggerInDb.TestSuites);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

        return await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
    }
}