using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Utils;

public abstract class ServiceTest : IClassFixture<MongoIntegrationTest>
{
    protected readonly MongoIntegrationTest Fixture;

    protected ServiceTest(MongoIntegrationTest fixture)
    {
        Fixture = fixture;

        Task.Run(() => Fixture.InitializeAsync()).Wait();
    }

    protected CommonEvent<T> EventFromJson<T>(string json)
    {
        return (CommonEvent<T>)JsonSerializer.Deserialize(json, typeof(CommonEvent<T>))!;
    }
}