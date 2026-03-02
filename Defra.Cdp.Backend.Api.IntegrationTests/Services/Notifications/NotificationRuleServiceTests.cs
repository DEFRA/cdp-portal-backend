using Amazon.S3;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
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
            EventType = NotificationTypes.TestPassed,
            Environment = "dev"
        };

        var rule2 = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationTypes.TestFailed,
            Environment = "test"
        };

        await rulesService.SaveAsync(rule1, ct);
        await rulesService.SaveAsync(rule2, ct);

        var rulesPassed = await rulesService.FindRule(rule1.RuleId , ct);
        var rulesFailed = await rulesService.FindRule(rule2.RuleId, ct);
        
        Assert.NotNull(rulesPassed);
        Assert.Equivalent(rulesPassed, rule1);
       
        Assert.NotNull(rulesFailed);
        Assert.Equivalent(rule2, rulesFailed);
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
            EventType = NotificationTypes.TestPassed,
            Environment = "dev"
        };

        // Create the rule
        await rulesService.SaveAsync(rule1, ct);
        var savedRule = await rulesService.FindRule(rule1.RuleId, ct);
        Assert.NotNull(savedRule);
        Assert.Equivalent(rule1, savedRule);
        
        // Find the rule by entity
        var byEntity = await rulesService.FindByEntity(rule1.Entity, ct);
        Assert.Single(byEntity);
        
        // Match the rule to an alert
        var matched = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = rule1.Entity, Environment = rule1.Environment, RunId = "123" }, ct);
        Assert.Single(matched);
        Assert.Equivalent(rule1, matched[0]);
        
        // Dont match unrelated events 
        var notMatched = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = "bar-backend", Environment = rule1.Environment, RunId = "444" }, ct);
        Assert.Empty(notMatched);


        // Update the rule
        rule1 = rule1 with { Environment = "test" };
        
        await rulesService.UpdateAsync(rule1, ct);
        var updatedRule = await rulesService.FindRule(rule1.RuleId, ct);
        Assert.NotNull(updatedRule);
        Assert.Equivalent(rule1, updatedRule);

        // Delete rule
        var deletedOk = await rulesService.DeleteAsync(rule1.RuleId, ct);
        Assert.True(deletedOk);
        
        var deletedRule = await rulesService.FindRule(rule1.RuleId, ct);
        Assert.Null(deletedRule);
    }
}