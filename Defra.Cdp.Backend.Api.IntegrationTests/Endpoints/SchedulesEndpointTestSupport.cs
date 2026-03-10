using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Scheduler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class SchedulesEndpointTestSupport : MongoTestSupport
{
    private readonly IHost _host;

    private const string MockToken =
        "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1lIjoiQWRtaW4gVXNlciIsIm9pZCI6IjkwNTUyNzk0LTA2MTMtNDAyMy04MTlhLTUxMmFhOWQ0MDAyMyJ9.";
    
    public SchedulesEndpointTestSupport(MongoContainerFixture fixture) : base(fixture)
    {
        IEntitiesService entitiesService = new EntitiesService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ISchedulerService, SchedulerService>();
                    services.AddSingleton(entitiesService);
                    services.AddSingleton<IMongoDbClientFactory>(CreateMongoDbClientFactory());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => { endpoints.MapSchedulesEndpoint(); });
                });
            })
            .Start();

        var tasks = EntityTestData().Select(e => entitiesService.Create(e, CancellationToken.None));
        Task.WaitAll(tasks.ToArray(), CancellationToken.None);
    }

    [Fact]
    public async Task Should_create_test_suite_task_schedule()
    {
        var client = _host.GetTestClient();

        var json = """
                   {
                     "task": {
                       "type": "DeployTestSuite",
                       "entityId": "my-test-entity",
                       "environment": "dev",
                       "cpu": 512,
                       "memory": 1024
                     },
                     "config": {
                       "frequency": "WEEKLY",
                       "time": "13:45",
                       "daysOfWeek": ["Friday", "Saturday"]
                     }
                   }
                   """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/schedules")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MockToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but got {response.StatusCode}. json: {body}");
    }

    private static Entity[] EntityTestData()
    {
        return
        [
            new Entity
            {
                Name = "my-test-entity",
                Teams =
                [
                    new Team { Name = "Platform", TeamId = "platform" }, new Team { Name = "Tenant", TeamId = "tenant" }
                ],
                Status = Status.Created,
                Type = Type.TestSuite
            }
        ];
    }
}
