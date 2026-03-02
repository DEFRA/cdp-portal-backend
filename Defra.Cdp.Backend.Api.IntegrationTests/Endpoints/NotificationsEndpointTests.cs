using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
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
    private readonly INotificationRuleService _ruleService;

    // Create Server
    private readonly TestServer _server;

    public NotificationsEndpointTests(MongoContainerFixture fixture) : base(fixture)
    {
        _ruleService = new NotificationRuleService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddScoped<IValidator<CreateNotificationRuleRequest>, CreateNotificationRuleRequestValidator>();
                services.AddSingleton(_ruleService);
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
        var request = new CreateNotificationRuleRequest { Entity = "foo-bar", EventType = "TestRunPassed", Conditions = new Dictionary<string, string> { { "Environment", "dev" }}};
        var result = await client.PostAsJsonAsync("/notifications", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }

    
}