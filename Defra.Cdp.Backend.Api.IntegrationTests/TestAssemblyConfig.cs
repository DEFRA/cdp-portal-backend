using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

[assembly: CollectionBehavior(MaxParallelThreads = 4, DisableTestParallelization = false)]
[assembly: AssemblyFixture(typeof(MongoContainerFixture))] 