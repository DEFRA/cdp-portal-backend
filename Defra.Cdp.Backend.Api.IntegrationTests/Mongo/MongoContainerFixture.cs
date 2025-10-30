using Testcontainers.MongoDb;
using Testcontainers.Xunit;
using Xunit.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public sealed class MongoContainerFixture(IMessageSink messageSink) : ContainerFixture<MongoDbBuilder, MongoDbContainer>(messageSink)
{
    protected override MongoDbBuilder Configure(MongoDbBuilder builder)
    {
        return builder
            .WithImage("mongo:6.0");
    }

}
