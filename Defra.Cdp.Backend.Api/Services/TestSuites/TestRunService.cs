using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunService
{
    public Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken);
    public Task<List<TestRun>> FindTestRunsForTestSuite(string suite, CancellationToken cancellationToken, int limit);
    public Task<TestRun?> FindByTaskArn(string taskArn, CancellationToken cancellationToken);
    public Task CreateTestRun(TestRun testRun, CancellationToken cancellationToken);
    public Task<TestRun?> Link(TestRunMatchIds ids, string taskArn, CancellationToken cancellationToken);
    public Task UpdateStatus(string taskArn, string status, DateTime ecsEventTimestamp , CancellationToken cancellationToken);
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

        return new List<CreateIndexModel<TestRun>> { runIdIndex, suiteIndex, arnIndex };
    }

    public async Task<TestRun?> FindTestRun(string runId, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<TestRun>> FindTestRunsForTestSuite(string suite, CancellationToken cancellationToken, int limit)
    {
        return await Collection.Find(t => t.TestSuite == suite).SortByDescending(t=>t.Created).Limit(limit).ToListAsync(cancellationToken);
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
        var transaction = await Collection.Database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            // Until we setup the lambda like we have with other deployments, we're going to do a 'best effort' match,
            // which basically means taking the first unlinked record that happened up to N minutes before we received the
            // ECS event.
            var testRun = await Collection
                .Find(t => 
                    t.Environment == ids.Environment 
                    && t.TestSuite   == ids.TestSuite 
                    && t.Created     <= ids.EventTime 
                    && t.Created     >= ids.EventTime.Subtract(_ecsLinkTimeWindow) 
                    && t.TaskArn     == null)
                .SortBy(t => t.Created)
                .FirstOrDefaultAsync(cancellationToken);

            if (testRun == null)
            {
                Logger.LogWarning("Failed to link test suite ${env} ${testSuite} to ${taskArn}", ids.Environment, ids.TestSuite, taskArn);
                await transaction.AbortTransactionAsync(cancellationToken);
                return null;
            }
            
            var update = Builders<TestRun>
                .Update
                .Set(d => d.TaskArn, taskArn);
            
            await Collection.UpdateOneAsync(d => d.RunId == testRun.RunId, update, cancellationToken: cancellationToken);
            await transaction.CommitTransactionAsync(cancellationToken);
            
            Logger.LogInformation("Linked TestRun ${runId} to taskArn ${taskArn}", testRun.RunId, taskArn);
        }
        catch (Exception e)
        {
            Logger.LogError("Error while linking test job to taskArn ${taskArn}: ${e}", taskArn, e);
            await transaction.AbortTransactionAsync(cancellationToken);
        }

        return null;
    }

    public async Task UpdateStatus(string taskArn, string status, DateTime ecsEventTimestamp, CancellationToken cancellationToken)
    {
        var update = Builders<TestRun>.Update
            .Set(d => d.TaskStatus, status)
            .Set(d => d.TaskLastUpdate, ecsEventTimestamp);
        await Collection.UpdateOneAsync(t => t.TaskArn == taskArn, update, cancellationToken: cancellationToken);
    }
}