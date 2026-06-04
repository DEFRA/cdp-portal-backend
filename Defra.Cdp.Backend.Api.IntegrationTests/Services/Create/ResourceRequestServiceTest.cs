using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create;

public class ResourceRequestServiceTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    private static readonly UserDetails TestUser = new() { Id = "user-123", DisplayName = "Test User" };

    private static readonly GitHubTriggerWorkflowResponse TestWorkflow = new()
    {
        WorkflowRunId = 25552427454L,
        WorkflowRunUrl = "https://api.github.com/repos/DEFRA/cdp-tenant-config/actions/runs/25552427454",
        WorkflowRunHtmlUrl = "https://github.com/DEFRA/cdp-tenant-config/actions/runs/25552427454"
    };

    [Fact]
    public async Task Should_persist_resource_request_with_entity_user_and_workflow_link()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request = new CreateS3BucketRequest
        {
            Service = "foo-backend",
            BucketName = "my-test-bucket",
            Environment = "dev"
        };

        var before = DateTime.UtcNow;
        await service.RecordRequest("foo-backend", TestUser, [request], TestWorkflow, CancellationToken.None);
        var after = DateTime.UtcNow;

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var records = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(records);

        var record = records.First();
        Assert.Equal("foo-backend", record.EntityName);
        Assert.Equivalent(TestUser, record.RequestedBy);
        Assert.InRange(record.RequestedAt, before, after);

        Assert.Single(record.Resources);

        var resourceDoc = record.Resources[0].AsBsonDocument;
        Assert.Equal("s3", resourceDoc["resourceType"].AsString);
        Assert.Equal("foo-backend", resourceDoc["service"].AsString);
        Assert.Equal("my-test-bucket", resourceDoc["bucketName"].AsString);
        Assert.Equal("dev", resourceDoc["environment"].AsString);

        Assert.NotNull(record.Workflow);
        Assert.Equal(TestWorkflow.WorkflowRunId, record.Workflow["workflow_run_id"].AsInt64);
        Assert.Equal(TestWorkflow.WorkflowRunUrl, record.Workflow["run_url"].AsString);
        Assert.Equal(TestWorkflow.WorkflowRunHtmlUrl, record.Workflow["html_url"].AsString);
    }

    [Fact]
    public async Task Should_persist_multiple_resources_in_single_record()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        List<CreateResourceRequest> resources =
        [
            new CreateS3BucketRequest { Service = "multi-svc", BucketName = "bucket-one", Environment = "dev" },
            new CreateS3BucketRequest { Service = "multi-svc", BucketName = "bucket-two", Environment = "dev" }
        ];

        await service.RecordRequest("multi-svc", TestUser, resources, TestWorkflow, CancellationToken.None);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, record.Resources.Count);
        Assert.Equal(1, await collection.CountDocumentsAsync(
            Builders<ResourceRequestRecord>.Filter.Empty,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_persist_record_with_null_user_when_no_auth_token_provided()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request = new CreateS3BucketRequest
        {
            Service = "anon-svc",
            BucketName = "anon-bucket",
            Environment = "test"
        };

        await service.RecordRequest("anon-svc", null, [request], TestWorkflow, CancellationToken.None);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal("anon-svc", record.EntityName);
        Assert.Null(record.RequestedBy);
    }
}