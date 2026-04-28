using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Scheduler.TestSuiteDeployment;
using Defra.Cdp.Backend.Api.Services.Usage;
using MongoDB.Bson;
using MongoDB.Driver;
using InsertOneOptions = MongoDB.Driver.InsertOneOptions;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunService
{
    Task<bool> AnyTestRunExists(string suite, string environment, string deploymentId, CancellationToken ct);
    
    Task CreateTestRun(TestRun testRun, CancellationToken ct);
    
    Task<TestRun?> FindTestRun(string runId, CancellationToken ct);
    
    Task<Paginated<TestRun>> FindTestRuns(TestRunMatcher matcher, int offset, int page, int size, CancellationToken ct);
    
    Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken ct);

    Task<TestRunSettings?> FindTestRunSettings(string name, string environment, CancellationToken ct);
    
    Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken ct);
    
    Task UpdateStatus(string taskArn, string taskStatus, string? testStatus, DateTime ecsEventTimestamp,
        List<FailureReason> failureReasons, CancellationToken ct);
    
    Task UpdateSnapshots(string runId, IEnumerable<string> snapshotUrls, CancellationToken ct);
}

public class TestRunService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TestRun>(connectionFactory, CollectionName, loggerFactory), ITestRunService, IStatsReporter
{
    private const string CollectionName = "testruns";
    private readonly TimeSpan _ecsLinkTimeWindow = TimeSpan.FromSeconds(120); // how many seconds between requesting the test run and the first ECS event
    public const int DefaultPageSize = 50;
    public const int DefaultPage = 1;

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

    public async Task<Paginated<TestRun>> FindTestRuns(TestRunMatcher matcher, int offset, int page, int size, CancellationToken ct)
    {
        var filter = matcher.Match();
        var testRuns = await Collection
            .Find(filter)
            .Skip(offset + size * (page - DefaultPage))
            .Limit(size)
            .SortByDescending(t => t.Created)
            .ToListAsync(ct);
        
        var totalTestRuns = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalTestRuns / size));

        return new Paginated<TestRun>(testRuns, page, size, totalPages);
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

        var filter = Builders<TestRun>.Filter.Eq(t => t.TaskArn, taskArn);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task UpdateSnapshots(string runId, IEnumerable<string> snapshotUrls, CancellationToken ct)
    {
        var filter = Builders<TestRun>.Filter.Eq(t => t.RunId, runId);
        
        var update = Builders<TestRun>
            .Update
            .Set(t => t.SnapshotUrls, snapshotUrls);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
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

    public async Task<bool> AnyTestRunExists(
        string suite,
        string environment,
        string deploymentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite) ||
            string.IsNullOrWhiteSpace(environment) ||
            string.IsNullOrWhiteSpace(deploymentId))
        {
            return false;
        }

        var builder = Builders<TestRun>.Filter;

        var filter = builder.Eq(t => t.TestSuite, suite) &
                     builder.Eq(t => t.Environment, environment) &
                     builder.Eq(t => t.Deployment!.DeploymentId, deploymentId);

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task ReportStats(ICloudWatchMetricsService metrics, CancellationToken cancellationToken)
    {
        var totalTestRuns =
            await Collection.CountDocumentsAsync(FilterDefinition<TestRun>.Empty, null, cancellationToken); 

        metrics.RecordCount("TestRunsTotal", null, totalTestRuns);
        
        // Runs by environment
        var pipelineByEnv = new EmptyPipelineDefinition<TestRun>()
            .Group(x => x.Environment, g => new 
            { 
                Environment = g.Key, 
                Count = g.Count() 
            });
        var totalTestRunsByEnv = await Collection.Aggregate(pipelineByEnv, null, cancellationToken).ToListAsync(cancellationToken);
        
        foreach (var testRun in totalTestRunsByEnv)
        {
            metrics.RecordCount("TestRunsByEnv", new Dictionary<string, string>(){ { "Environment", testRun.Environment  }}, testRun.Count);
        }
        
        // Automated runs
        var pipelineByRunType = new EmptyPipelineDefinition<TestRun>()
            .Match(x => x.User.Id == AutoTestRunConstants.UserId || x.User.Id == ScheduledTestRunConstants.UserId)
            .Group(x => x.User.Id, g => new 
            { 
                UserId = g.Key, 
                Count = g.Count() 
            });
        
        var totalAutomaticTestRuns = await Collection.Aggregate(pipelineByRunType, null, cancellationToken).ToListAsync(cancellationToken);

        foreach (var testRun in totalAutomaticTestRuns)
        {
            switch (testRun.UserId)
            {
                case AutoTestRunConstants.UserId:
                    metrics.RecordCount("TestRunsAutomated", new Dictionary<string, string>{ { "Automation", "deployment"  }}, testRun.Count);
                    break;
                case ScheduledTestRunConstants.UserId:
                    metrics.RecordCount("TestRunsAutomated", new Dictionary<string, string>{ { "Automation", "scheduler"  }}, testRun.Count);
                    break;
            }
        }
    }
}