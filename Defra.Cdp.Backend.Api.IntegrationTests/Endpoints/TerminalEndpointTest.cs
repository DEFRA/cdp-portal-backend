using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Terminal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class TerminalEndpointTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task TerminalEndpointShowRecordSessionToMongo()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TerminalEndpointTest");
        var loggerFactory = new LoggerFactory();
        var terminalService = new TerminalService(mongoFactory, loggerFactory); 
        
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<ITerminalService>(terminalService);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapTerminalEndpoint();
                });
            });

        // Create Server
        var server = new TestServer(builder);
        var client = server.CreateClient();

        var session = new TerminalSession { Token = "123456", Environment = "prod", Service = "foo-backend", User = new UserDetails{ DisplayName = "user1", Id = "1"}};
        var response = await client.PostAsJsonAsync("/terminal", session);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var col = mongoFactory.GetCollection<TerminalSession>(TerminalService.CollectionName);
        var fromDatabase = await col.Find(t => t.Token == session.Token).ToListAsync(CancellationToken.None);
        Assert.Single(fromDatabase);
        var saved = fromDatabase.First();

        Assert.Equal(session.Service, saved.Service);
        Assert.Equal(session.Environment, saved.Environment);
        Assert.Equal(session.Token, saved.Token);
        Assert.Equal(session.User.DisplayName, saved.User.DisplayName);
        Assert.Equal(session.User.Id, saved.User.Id);
        // Mongo doesn't store dates with the precision as datetime.utcnow
        Assert.InRange(saved.Requested, session.Requested.Subtract(TimeSpan.FromMilliseconds(1)), session.Requested.Add(TimeSpan.FromMilliseconds(1)));

    }
}