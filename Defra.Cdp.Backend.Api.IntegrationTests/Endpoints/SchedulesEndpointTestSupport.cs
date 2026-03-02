using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class SchedulesEndpointTestSupport: MongoTestSupport
{
    // Create Server
    private readonly TestServer _server;

    public SchedulesEndpointTestSupport(MongoContainerFixture fixture) : base(fixture)
    {

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<ISchedulerService, SchedulerService>();
                services.AddSingleton<IMongoDbClientFactory>(CreateMongoDbClientFactory());
                
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapSchedulesEndpoint(); });
            });
        
        _server = new TestServer(builder);
    }
    
    [Fact]
    public async Task Should_create_test_suite_task_schedule()
    {
        var client = _server.CreateClient();
        
        var json = """
                   {
                     "task": {
                       "type": "DeployTestSuite",
                       "entityId": "cdp-example-tests",
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

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer",
                "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1lIjoiQWRtaW4gVXNlciIsIm9pZCI6IjkwNTUyNzk0LTA2MTMtNDAyMy04MTlhLTUxMmFhOWQ0MDAyMyJ9.");
        
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine(body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201 Created but got {response.StatusCode}. json: {body}");
    }
    
    private static string[] TestData()
    {
        return [];
    }

}