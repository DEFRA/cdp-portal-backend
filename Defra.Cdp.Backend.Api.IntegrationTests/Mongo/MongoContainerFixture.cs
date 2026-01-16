using Testcontainers.MongoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;


namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public sealed class MongoContainerFixture(IMessageSink messageSink) : ContainerFixture<MongoDbBuilder, MongoDbContainer>(messageSink)
{
    protected override MongoDbBuilder Configure(MongoDbBuilder builder)
    {
        return builder
            .WithImage("mongo:7.0");
    }

}