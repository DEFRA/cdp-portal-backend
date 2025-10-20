using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;

public interface IAutoTestRunTriggerService
{
    Task<AutoTestRunTrigger?> FindForService(string serviceName, CancellationToken cancellationToken);

    Task<AutoTestRunTrigger?> SaveTrigger(AutoTestRunTriggerDto autoTestRunTrigger,
        CancellationToken cancellationToken);

    Task<AutoTestRunTrigger?> RemoveTestRun(AutoTestRunTriggerDto autoTestRunTrigger,
        CancellationToken cancellationToken);

    Task<AutoTestRunTrigger?> UpdateTestRun(AutoTestRunTriggerDto autoTestRunTrigger,
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

    public async Task<AutoTestRunTrigger?> SaveTrigger(AutoTestRunTriggerDto autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating auto test run config {TestSuite} (profile: {Profile}) for service: {Service}",
            autoTestRunTrigger.TestSuite,
            autoTestRunTrigger.Profile,
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        var suites = triggerInDb?.TestSuites ?? new Dictionary<string, List<TestSuiteRunConfig>>();

        return await UpsertSuites(autoTestRunTrigger, suites, cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> RemoveTestRun(AutoTestRunTriggerDto autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Removing test run config {TestSuite} (profile: {Profile}) for service: {Service}",
            autoTestRunTrigger.TestSuite,
            autoTestRunTrigger.Profile,
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        if (triggerInDb == null) return null;

        var suites = triggerInDb.TestSuites;

        if (suites.TryGetValue(autoTestRunTrigger.TestSuite, out var configs))
        {
            var idx = configs.FindIndex(c => c.Profile == autoTestRunTrigger.Profile);
            if (idx >= 0)
            {
                configs.RemoveAt(idx);
                if (configs.Count == 0)
                {
                    suites.Remove(autoTestRunTrigger.TestSuite);
                }
            }
        }

        var filter = Builders<AutoTestRunTrigger>.Filter.Eq(t => t.ServiceName, autoTestRunTrigger.ServiceName);
        var update = Builders<AutoTestRunTrigger>.Update
            .Set(t => t.TestSuites, suites)
            .SetOnInsert(t => t.ServiceName, autoTestRunTrigger.ServiceName);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

        return await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);
    }

    public async Task<AutoTestRunTrigger?> UpdateTestRun(AutoTestRunTriggerDto autoTestRunTrigger,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating test run config {TestSuite} (profile: {Profile}) for service: {Service}",
            autoTestRunTrigger.TestSuite,
            autoTestRunTrigger.Profile,
            autoTestRunTrigger.ServiceName);

        var triggerInDb = await FindForService(autoTestRunTrigger.ServiceName, cancellationToken);

        _logger.LogInformation("DB trigger is: {TriggerInDb}", triggerInDb);

        if (triggerInDb == null) return null;

        var suites = triggerInDb.TestSuites;

        return await UpsertSuites(autoTestRunTrigger, suites, cancellationToken);
    }

    private static void UpsertConfig(Dictionary<string, List<TestSuiteRunConfig>> suites, AutoTestRunTriggerDto dto)
    {
        if (!suites.TryGetValue(dto.TestSuite, out var configs))
        {
            configs = [];
            suites[dto.TestSuite] = configs;
        }

        var idx = configs.FindIndex(c => c.Profile == dto.Profile);

        if (dto.Environments.Count == 0)
        {
            if (idx >= 0) configs.RemoveAt(idx);
            if (configs.Count == 0) suites.Remove(dto.TestSuite);
            return;
        }

        var updated = new TestSuiteRunConfig
        {
            Profile = dto.Profile,
            Environments = dto.Environments
        };

        if (idx >= 0) configs[idx] = updated; else configs.Add(updated);
    }

    private async Task<AutoTestRunTrigger?> UpsertSuites(AutoTestRunTriggerDto trigger,
        Dictionary<string, List<TestSuiteRunConfig>> suites,
        CancellationToken cancellationToken)
    {
        UpsertConfig(suites, trigger);
        
        var filter = Builders<AutoTestRunTrigger>.Filter.Eq(t => t.ServiceName, trigger.ServiceName);
        var update = Builders<AutoTestRunTrigger>.Update
            .Set(t => t.TestSuites, suites)
            .SetOnInsert(t => t.ServiceName, trigger.ServiceName);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        return await FindForService(trigger.ServiceName, cancellationToken);
    }
}