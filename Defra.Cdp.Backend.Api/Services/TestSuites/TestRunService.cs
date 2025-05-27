using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;
using InsertOneOptions = MongoDB.Driver.InsertOneOptions;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunService
{
    public Task<TestRun?> FindTestRun(string runId, CancellationToken ct);

    public Task<Paginated<TestRun>> FindTestRunsForTestSuite(string suite, int offset = 0, int page = 0, int size = 0,
        CancellationToken ct = new());

    public Task<Dictionary<string, TestRun>> FindLatestTestRuns(CancellationToken ct);
    public Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken ct);
    public Task CreateTestRun(TestRun testRun, CancellationToken ct);
    public Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken ct);

    public Task UpdateStatus(string taskArn, string taskStatus, string? testStatus, DateTime ecsEventTimestamp,
        List<FailureReason> failureReasons, CancellationToken ct);

    public Task<TestRunSettings?> FindTestRunSettings(string name, string environment, CancellationToken ct);

    Task Decommission(string serviceName, CancellationToken ct);
}

public class TestRunService : MongoService<TestRun>, ITestRunService
{
    private const string CollectionName = "testruns";
    private readonly TimeSpan _ecsLinkTimeWindow = TimeSpan.FromSeconds(120); // how many seconds between requesting the test run and the first ECS event
    public static readonly int DefaultPageSize = 50;
    public static readonly int DefaultPage = 1;

    public TestRunService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, CollectionName, loggerFactory) { }

    protected override List<CreateIndexModel<TestRun>> DefineIndexes(IndexKeysDefinitionBuilder<TestRun> builder)
    {
        var runIdIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.RunId));
        var suiteIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.TestSuite));
        var arnIndex = new CreateIndexModel<TestRun>(builder.Ascending(t => t.TaskArn));

        return [runIdIndex, suiteIndex, arnIndex];
    }

    public async Task<TestRun?> FindTestRun(string runId, CancellationToken ct)
    {
        return await Collection.Find(t => t.RunId == runId).FirstOrDefaultAsync(ct);
    }

    public async Task<Paginated<TestRun>> FindTestRunsForTestSuite(
        string suite,
        int offset,
        int page,
        int size,
        CancellationToken ct)
    {
        var builder = Builders<TestRun>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(suite))
        {
            var testRunFilter = builder.Where(t => t.TestSuite == suite);
            filter &= testRunFilter;
        }

        var testRuns = await Collection
            .Find(t => t.TestSuite == suite)
            .Skip(offset + size * (page - DefaultPage))
            .Limit(size)
            .SortByDescending(t => t.Created)
            .ToListAsync(ct);

        var totalTestRuns = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalTestRuns / size));

        return new Paginated<TestRun>(testRuns, page, size, totalPages);
    }

    public async Task<Dictionary<string, TestRun>> FindLatestTestRuns(CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<TestRun>()
            .Sort(new SortDefinitionBuilder<TestRun>().Descending(d => d.Created)).Group(t => t.TestSuite,
                grp => new { Root = grp.First() }).Project(grp => grp.Root);

        var runs = await Collection.AggregateAsync(pipeline, cancellationToken: ct);
        return runs.ToEnumerable(ct).ToDictionary(d => d.TestSuite, d => d);
    }

    public async Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken ct)
    {
        return await Collection.Find(t => t.TaskArn == taskArn).FirstOrDefaultAsync(ct);
    }

    public async Task CreateTestRun(TestRun testRun, CancellationToken ct)
    {
        testRun.TaskStatus = "starting";
        await Collection.InsertOneAsync(testRun, new InsertOneOptions(), ct);
    }

    public async Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<TestRun>();

        var filter = fb.And(
            fb.Eq(t => t.TestSuite, ids.TestSuite),
            fb.Eq(t => t.Environment, ids.Environment),
            fb.Lte(t => t.Created, ids.EventTime),
            fb.Gte(t => t.Created, ids.EventTime.Subtract(_ecsLinkTimeWindow)),
            fb.Eq(t => t.TaskArn, null)
        );

        var update = Builders<TestRun>
            .Update
            .Set(d => d.TaskArn, taskArn);

        return await Collection.FindOneAndUpdateAsync(filter, update, cancellationToken: ct);
    }

    public async Task UpdateStatus(string taskArn, string taskStatus, string? testStatus, DateTime ecsEventTimestamp,
        List<FailureReason> failureReasons, CancellationToken ct)
    {
        var update = Builders<TestRun>
            .Update
            .Set(t => t.TaskStatus, taskStatus).Set(t => t.TaskLastUpdate, ecsEventTimestamp).Set(
                t => t.FailureReasons,
                failureReasons);

        if (testStatus != null)
        {
            update = update.Set(t => t.TestStatus, testStatus);
        }

        await Collection.UpdateOneAsync(t => t.TaskArn == taskArn, update,
            cancellationToken: ct);
    }

    public async Task Decommission(string serviceName, CancellationToken ct)
    {
        await Collection.DeleteManyAsync(t => t.TestSuite == serviceName, ct);
    }

    public async Task<TestRunSettings?> FindTestRunSettings(string name, string environment, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<TestRun>();
        var filter = fb.And(fb.Eq(t => t.TestSuite, name), fb.Eq(t => t.Environment, environment));
        var sort = new SortDefinitionBuilder<TestRun>().Descending(t => t.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Project(t => new TestRunSettings { Cpu = t.Cpu, Memory = t.Memory })
            .FirstOrDefaultAsync(ct);
    }

}