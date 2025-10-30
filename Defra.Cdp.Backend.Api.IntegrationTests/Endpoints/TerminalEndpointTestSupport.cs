using System.Net;
using System.Net.Http.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Audit;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Terminal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using Audit = Defra.Cdp.Backend.Api.Services.Audit.Audit;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class TerminalEndpointTestSupport(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task TerminalEndpointShowRecordSessionToMongo()
    {

        var mongoFactory = CreateMongoDbClientFactory();
        var loggerFactory = new NullLoggerFactory();
        var terminalService = new TerminalService(mongoFactory, loggerFactory);
        var cwMock = new Mock<ICloudWatchMetricsService>();
        var auditService = new AuditService(mongoFactory, cwMock.Object, loggerFactory);

        cwMock
            .Setup(s => s.RecordCount(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<double>()))
            .Verifiable();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<ITerminalService>(terminalService);
                services.AddSingleton<IAuditService>(auditService);
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

        var prodSession = new TerminalSession { Token = "123456", Environment = "prod", Service = "foo-backend", User = new UserDetails { DisplayName = "user1", Id = "1" } };
        var prodResponse = await client.PostAsJsonAsync("/terminals", prodSession);
        Assert.Equal(HttpStatusCode.Created, prodResponse.StatusCode);
        var testSession = new TerminalSession { Token = "123456", Environment = "test", Service = "foo-backend", User = new UserDetails { DisplayName = "user1", Id = "1" } };
        var testResponse = await client.PostAsJsonAsync("/terminals", testSession);
        Assert.Equal(HttpStatusCode.Created, testResponse.StatusCode);


        var terminalCollections = mongoFactory.GetCollection<TerminalSession>(TerminalService.CollectionName);
        var fromDatabase = await terminalCollections.Find(t => t.Token == prodSession.Token).ToListAsync(CancellationToken.None);
        Assert.Equal(2, fromDatabase.Count);
        var saved = fromDatabase.First();

        Assert.Equal(prodSession.Service, saved.Service);
        Assert.Equal(prodSession.Environment, saved.Environment);
        Assert.Equal(prodSession.Token, saved.Token);
        Assert.Equal(prodSession.User.DisplayName, saved.User.DisplayName);
        Assert.Equal(prodSession.User.Id, saved.User.Id);
        // Mongo doesn't store dates with the precision as datetime.utcnow
        Assert.InRange(saved.Requested, prodSession.Requested.Subtract(TimeSpan.FromMilliseconds(1)), prodSession.Requested.Add(TimeSpan.FromMilliseconds(1)));

        var auditCollections = mongoFactory.GetCollection<Audit>(AuditService.CollectionName);
        var auditsFromDb = await auditCollections.Find(Builders<Audit>.Filter.Empty).ToListAsync(CancellationToken.None);
        Assert.Single(auditsFromDb);
        var audit = auditsFromDb.First();

        Assert.Equal("breakGlass", audit.Category);
        Assert.Equal("TerminalAccess", audit.Action);
        Assert.Equal(prodSession.User.DisplayName, audit.PerformedBy.DisplayName);
        Assert.InRange(audit.PerformedAt, DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1)), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));
        Assert.Equal(prodSession.Environment, audit.Details["environment"].AsString);
        Assert.Equal(prodSession.Service, audit.Details["service"].AsString);

    }
}