using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.EventHistory;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.EventHistory;

public class EventHistoryRepositoryTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task Persists_json_events()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var factory = new EventHistoryFactory(mongoFactory, new NullLoggerFactory());
        var service = factory.Create<EventHistoryRepositoryTest>();


        var msg = JsonDocument.Parse("{\"foo\": 123, \"bar\": \"baz\"}");
        await service.PersistEvent("1234", msg.RootElement, TestContext.Current.CancellationToken);

        var colName = nameof(EventHistoryRepositoryTest).ToLower() + "_events";

        var col = mongoFactory.GetCollection<BsonDocument>(colName);
        var result = col.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefault(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("1234", result.GetValue("messageId").AsString);
        var savedEvent = result.GetValue("event").AsBsonDocument;
        Assert.Equal(123, savedEvent.GetValue("foo").AsInt32);
        Assert.Equal("baz", savedEvent.GetValue("bar").AsString);
    }
}