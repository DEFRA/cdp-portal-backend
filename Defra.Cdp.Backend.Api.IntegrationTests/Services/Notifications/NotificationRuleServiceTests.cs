using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Notifications;

public class NotificationRuleServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task test_load_and_save_rules()
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
    public async Task test_rule_crud()
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
        {
            var notMatched = await rulesService.FindMatchingRules(
                new TestRunPassedEvent { Entity = "bar-backend", Environment = rule1.Environment, RunId = "444" }, ct);
            Assert.Empty(notMatched);
        }
        
        {
            var notMatched = await rulesService.FindMatchingRules(
                new TestRunPassedEvent { Entity = rule1.Entity, Environment = "prod", RunId = "444" }, ct);
            Assert.Empty(notMatched);
        }

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


    [Fact]
    public async Task test_wildcard_environment_rule_matching()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var rulesService = new NotificationRuleService(connectionFactory, new NullLoggerFactory());
        var wildcardRule = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationTypes.TestPassed
        };
        
        await rulesService.SaveAsync(wildcardRule, ct);


        foreach (var env in CdpEnvironments.Environments)
        {
            var matched = await rulesService.FindMatchingRules(
                new TestRunPassedEvent { Entity = wildcardRule.Entity, Environment = env, RunId = "444" }, ct);
            Assert.Single(matched);
            Assert.Equivalent(wildcardRule, matched[0]);
        }
    }
    
    [Fact]
    public async Task test_normal_rule_matching()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var rulesService = new NotificationRuleService(connectionFactory, new NullLoggerFactory());
        var normalRule = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationTypes.TestPassed,
            Environment = "dev"
        };
       
        await rulesService.SaveAsync(normalRule, ct);

        
        
        var matched = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = normalRule.Entity, Environment = normalRule.Environment, RunId = "444" }, ct);
        Assert.Single(matched);
        Assert.Equivalent(normalRule, matched[0]);
    
        var notMatchedEnv = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = normalRule.Entity, Environment = "test", RunId = "444" }, ct);
        Assert.Empty(notMatchedEnv);
        
        var notMatchedEntity = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = "baz-backend", Environment = normalRule.Environment, RunId = "444" }, ct);
        Assert.Empty(notMatchedEntity);
    }
    
    [Fact]
    public async Task test_doesnt_match_disabled_rule()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var rulesService = new NotificationRuleService(connectionFactory, new NullLoggerFactory());
        var normalRule = new NotificationRule
        {
            Entity = "foo",
            EventType = NotificationTypes.TestPassed,
            Environment = "dev",
            IsEnabled = false
        };
       
        await rulesService.SaveAsync(normalRule, ct);
       
        
        var matched = await rulesService.FindMatchingRules(
            new TestRunPassedEvent { Entity = normalRule.Entity, Environment = normalRule.Environment, RunId = "444" }, ct);
        Assert.Empty(matched);
    }
}