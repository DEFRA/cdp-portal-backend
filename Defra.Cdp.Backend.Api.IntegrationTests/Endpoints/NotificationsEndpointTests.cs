using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class NotificationsEndpointTests : MongoTestSupport
{
    // Create Server
    private readonly IHost _host;
    private readonly ISlackClient _slackClient;
    
    public NotificationsEndpointTests(MongoContainerFixture fixture) : base(fixture)
    {
        var mongodbFactory = CreateMongoDbClientFactory();
        INotificationRuleService ruleService = new NotificationRuleService(mongodbFactory, new NullLoggerFactory());
        _slackClient = Substitute.For<ISlackClient>();
        
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(ruleService);
                    services.AddSingleton(_slackClient);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => { endpoints.MapNotificationEndpoints(); });
                });
            })
            .Start();
        
    }

    [Fact]
    public async Task Should_create_new_rule_with_valid_payload()
    {
        var client = _host.GetTestClient();
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["dev"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_reject_new_rule_with_invalid_environment()
    {
        var client = _host.GetTestClient();
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestPassed, Environments = ["foo"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_reject_new_rule_with_invalid_type()
    {
        var client = _host.GetTestClient();
        var request = new CreateRuleRequest { EventType = "pigeon-alert", Environments = ["foo"] };
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
    
    [Fact]
    public async Task Should_update_and_delete_a_rule()
    {
        var client = _host.GetTestClient();
        
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

    [Fact]
    public async Task Should_send_test_message_to_channel()
    {
        var client = _host.GetTestClient();
        
        var request = new CreateRuleRequest { EventType = NotificationTypes.TestFailed, Environments = ["dev"], SlackChannel = "foo-channel"};
        var result = await client.PostAsJsonAsync("/entities/foo-bar/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.NotNull(result.Headers.Location);
        
        var rule = await client.GetFromJsonAsync<NotificationRule>(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.NotNull(rule);

        result = await client.PostAsync(result.Headers.Location + "/test", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        await _slackClient.Received().SendToChannel(Arg.Is(request.SlackChannel), Arg.Any<SlackMessageBody>(),
            Arg.Any<CancellationToken>());
    }
    
}
