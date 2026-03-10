using Testcontainers.MongoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;


namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public sealed class MongoContainerFixture(IMessageSink messageSink) : ContainerFixture<MongoDbBuilder, MongoDbContainer>(messageSink)
{

    protected override MongoDbBuilder Configure()
    {
        return new MongoDbBuilder("mongo:7.0");
    }
    
    [Obsolete("This method is obsolete and will be removed. Use the parameterless Configure() method and create the builder explicitly instead.")]
    protected override MongoDbBuilder Configure(MongoDbBuilder builder)
    {
        return builder
            .WithImage("mongo:7.0");
    }

}