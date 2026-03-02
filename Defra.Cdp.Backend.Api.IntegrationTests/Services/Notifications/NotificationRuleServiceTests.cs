using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Notifications;

public class NotificationRuleServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task TestSaveAndLoadRules()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var rulesService = new NotificationRuleService(connectionFactory, new NullLoggerFactory());

        var rule1 = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunPassed.Type,
            Conditions = new Dictionary<string, string> { { "Environment", "dev" } }
        };

        var rule2 = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunFailed.Type,
            Conditions = new Dictionary<string, string> { { "Environment", "test" } }
        };

        await rulesService.SaveAsync(rule1, ct);
        await rulesService.SaveAsync(rule2, ct);

        var rulesPassed = await rulesService.FindByEntityAndTypeAsync("foo", NotificationEventTypes.TestRunPassed.Type, ct);
        var rulesFailed = await rulesService.FindByEntityAndTypeAsync("foo", NotificationEventTypes.TestRunFailed.Type, ct);
        
        Assert.NotEmpty(rulesPassed);
        Assert.Equivalent(rule1, rulesPassed[0]);
        
        Assert.NotEmpty(rulesFailed);
        Assert.Equivalent(rule2, rulesFailed[0]);
    }
    
    [Fact]
    public async Task TestCrud()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var rulesService = new NotificationRuleService(connectionFactory, new NullLoggerFactory());

        var rule1 = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunPassed.Type,
            Conditions = new Dictionary<string, string> { { "Environment", "dev" } }
        };

        // Create the rule
        await rulesService.SaveAsync(rule1, ct);
        var savedRule = await rulesService.FindByEntity("foo", ct);
        Assert.NotEmpty(savedRule);
        Assert.Equivalent(rule1, savedRule[0]);

        // Update the rule
        rule1.Conditions["Profile"] = "smokeTest";
        await rulesService.UpdateAsync(rule1, ct);
        var updatedRule = await rulesService.FindByEntity("foo", ct);
        Assert.NotEmpty(updatedRule);
        Assert.Equivalent(rule1, updatedRule[0]);
        
        // Delete rule
        await rulesService.DeleteAsync(rule1.RuleId, ct);
        var deletedOk = await rulesService.DeleteAsync(rule1.RuleId, ct);
        Assert.True(deletedOk);
        
        var deletedRule = await rulesService.FindByEntity("foo", ct);
        Assert.Empty(deletedRule);
    }
}