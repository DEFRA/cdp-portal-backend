using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunService
{
    public Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken);
    public Task<List<TestRun>> FindTestRunsForTestSuite(string suite, CancellationToken cancellationToken);
    public Task CreateTestRun(TestRun testRun, CancellationToken cancellationToken);
}

public class TestRunService : MongoService<TestRun>, ITestRunService
{
    private const string CollectionName = "testruns";
    public TestRunService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, CollectionName, loggerFactory) {}

    protected override List<CreateIndexModel<TestRun>> DefineIndexes(IndexKeysDefinitionBuilder<TestRun> builder)
    {
        var runIdIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.RunId));
        var suiteIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.TestSuite));

        return new List<CreateIndexModel<TestRun>> { runIdIndex, suiteIndex };
    }

    public async Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<TestRun>> FindTestRunsForTestSuite(string suite, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.TestSuite == suite).ToListAsync(cancellationToken);
    }

    public async Task CreateTestRun(TestRun testRun, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(testRun, new InsertOneOptions(), cancellationToken);
    }
}