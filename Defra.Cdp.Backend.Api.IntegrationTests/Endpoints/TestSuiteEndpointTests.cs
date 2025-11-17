using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class TestSuiteEndpointTests : MongoTestSupport
{
    private readonly ITestRunService _testRunService;
    private readonly TestServer _server;
    
    public TestSuiteEndpointTests(MongoContainerFixture fixture) : base(fixture) {
    
        _testRunService = new TestRunService(CreateMongoDbClientFactory(), new NullLoggerFactory());
        
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(_testRunService);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapTestSuiteEndpoint(); });
            });
        _server = new TestServer(builder);
    }

    [Fact]
    public async Task test_creating_and_finding_test_runs()
    {
        var client = _server.CreateClient();
        var createTest = new TestRun { RunId = "1234", Environment = "prod", ConfigVersion = "00000000", TestSuite = "my-test-suite", User = new UserDetails { DisplayName = "user1", Id = "1" } };
        var resp = await client.PostAsJsonAsync("/test-run", createTest, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        
        var search = await client.GetAsync("/test-run/1234", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var run = await search.Content.ReadFromJsonAsync<TestRun>(TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal("1234", run.RunId);
    }
    
    [Fact]
    public async Task test_find_and_paginate()
    {
        var firstDate = new DateTime(2025, 1, 1);
        for (var i = 0; i < 16; i++)
        {
            await _testRunService.CreateTestRun(
                new TestRun { RunId = "pagination-" + i, Created = firstDate.AddHours(i), Environment = "infra-dev", TestSuite = "pagination", User = new UserDetails { DisplayName = "user1", Id = "1" }},
                TestContext.Current.CancellationToken);
        }
        
        var client = _server.CreateClient();
        
        // First page, should be fully populated
        var resp = await client.GetAsync("/test-run?name=pagination&size=10", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var runs = await resp.Content.ReadFromJsonAsync<Paginated<TestRun>>(TestContext.Current.CancellationToken);

        Assert.NotNull(runs);
        Assert.Equal(10, runs.PageSize);
        Assert.Equal(10, runs.Data.Count);
        
        // Second page, should have remaining 6 items
        resp = await client.GetAsync("/test-run?name=pagination&size=10&page=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        runs = await resp.Content.ReadFromJsonAsync<Paginated<TestRun>>(TestContext.Current.CancellationToken);
        Assert.NotNull(runs);
        Assert.Equal(10, runs.PageSize);
        Assert.Equal(6, runs.Data.Count);
    }
}