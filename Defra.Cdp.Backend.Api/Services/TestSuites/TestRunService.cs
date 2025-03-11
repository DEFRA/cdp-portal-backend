using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunService
{
    public Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken);
    public Task<List<TestRun>> FindTestRunsForTestSuite(string suite, int limit, CancellationToken cancellationToken);
    public Task<Dictionary<string, TestRun>> FindLatestTestRuns(CancellationToken cancellationToken);
    public Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken cancellationToken);
    public Task CreateTestRun(TestRun testRun, CancellationToken cancellationToken);
    public Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken cancellationToken);
    public Task UpdateStatus(string taskArn, string taskStatus, string? testStatus, DateTime ecsEventTimestamp, List<FailureReason> failureReasons, CancellationToken cancellationToken);
    Task Decommission(string serviceName, CancellationToken cancellationToken);
}

public class TestRunService : MongoService<TestRun>, ITestRunService
{
    private const string CollectionName = "testruns";

    private readonly TimeSpan _ecsLinkTimeWindow = TimeSpan.FromSeconds(120); // how many seconds between requesting the test run and the first ECS event
    
    public TestRunService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, CollectionName, loggerFactory) {}

    protected override List<CreateIndexModel<TestRun>> DefineIndexes(IndexKeysDefinitionBuilder<TestRun> builder)
    {
        var runIdIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.RunId));
        var suiteIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.TestSuite));
        var arnIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.TaskArn));

        return [runIdIndex, suiteIndex, arnIndex];
    }

    public async Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<TestRun>> FindTestRunsForTestSuite(string suite, int limit, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.TestSuite == suite).SortByDescending(t=>t.Created).Limit(limit).ToListAsync(cancellationToken);
    }

   public async Task<Dictionary<string, TestRun>> FindLatestTestRuns(CancellationToken cancellationToken)
   {
           var pipeline = new EmptyPipelineDefinition<TestRun>()
               .Sort(new SortDefinitionBuilder<TestRun>().Descending(d => d.Created))
               .Group(t => t.TestSuite, grp => new { Root = grp.First() })
               .Project(grp => grp.Root);

           var runs = await Collection.AggregateAsync(pipeline, cancellationToken: cancellationToken);
           return runs.ToEnumerable(cancellationToken).ToDictionary(d => d.TestSuite, d => d);
   }

    public async Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.TaskArn == taskArn).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateTestRun(TestRun testRun, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(testRun, new InsertOneOptions(), cancellationToken);
   }

    public async Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<TestRun>();

        var filter = fb.And(
            fb.Eq(t => t.TestSuite, ids.TestSuite),
            fb.Eq(t => t.Environment, ids.Environment),
            fb.Lte(t => t.Created, ids.EventTime),
            fb.Gte(t => t.Created,  ids.EventTime.Subtract(_ecsLinkTimeWindow)),
            fb.Eq(t => t.TaskArn, null)
        );

        var update = Builders<TestRun>
            .Update
            .Set(d => d.TaskArn, taskArn);

        return await Collection.FindOneAndUpdateAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateStatus(string taskArn, string taskStatus, string? testStatus, DateTime ecsEventTimestamp, List<FailureReason> failureReasons, CancellationToken cancellationToken)
    {
        var update = Builders<TestRun>.Update
            .Set(t => t.TaskStatus, taskStatus)
            .Set(t => t.TaskLastUpdate, ecsEventTimestamp)
            .Set(t => t.FailureReasons, failureReasons);

        if (testStatus != null)
        {
            update = update.Set(t => t.TestStatus, testStatus);
        }
        
        await Collection.UpdateOneAsync(t => t.TaskArn == taskArn, update, cancellationToken: cancellationToken);
    }

    public async Task Decommission(string serviceName, CancellationToken cancellationToken)
    {
        await Collection.DeleteManyAsync(t => t.TestSuite == serviceName, cancellationToken: cancellationToken);
    }
}