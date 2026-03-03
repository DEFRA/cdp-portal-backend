using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class NotificationsEndpointTests : MongoTestSupport
{
    // Create Server
    private readonly TestServer _server;

    public NotificationsEndpointTests(MongoContainerFixture fixture) : base(fixture)
    {
        INotificationRuleService ruleService = new NotificationRuleService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(ruleService);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapNotificationEndpoints(); });
            });

        _server = new TestServer(builder);
    }

    [Fact]
    public async Task Should_create_new_rule_with_valid_payload()
    {
        var client = _server.CreateClient();
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["dev"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_reject_new_rule_with_invalid_environment()
    {
        var client = _server.CreateClient();
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["foo"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_reject_new_rule_with_invalid_type()
    {
        var client = _server.CreateClient();
        var request = new CreateRuleRequest { EventType = "pigeon-alert", Environments = ["foo"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_update_and_delete_a_rule()
    {
        var client = _server.CreateClient();
        
        // Create
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["dev"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.NotNull(result.Headers.Location);
        
        // Get
        var rule = await client.GetFromJsonAsync<NotificationRule>(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.NotNull(rule);
        Assert.Equal(["dev"], rule.Environments);
        Assert.NotEqual("", rule.RuleId);
        
        // Update
        var updateRequest = new UpdateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["test"], IsEnabled = false };
        var updateResult = await client.PutAsJsonAsync($"/entities/foo-bar/notifications/{rule.RuleId}", updateRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateResult.StatusCode);
        
        // Get updated
        var updatedRule = await client.GetFromJsonAsync<NotificationRule>(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(["test"], updatedRule?.Environments);
        
        // Delete
        var deleteResult = await client.DeleteAsync(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleteResult.StatusCode);
        
        var missingRule = await client.GetAsync(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingRule.StatusCode);
    }
}