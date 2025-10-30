using System.Text.Json;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Utils;

public abstract class ServiceTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    protected CommonEvent<T> EventFromJson<T>(string json)
    {
        return FromJson<CommonEvent<T>>(json);
    }

    protected T FromJson<T>(string json)
    {
        return (T)JsonSerializer.Deserialize(json, typeof(T))!;
    }
}