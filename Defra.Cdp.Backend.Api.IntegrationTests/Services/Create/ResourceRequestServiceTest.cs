using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
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

        var request = new CreateTenantResourceRequest
        {
            S3Buckets = [ new CreateTenantS3Bucket
            {
                Service = "foo-backend",
                Name = "my-test-bucket",
                Environments = "dev"
            }]
        };

        var inputs = request.ToWorkflowInputs("123", "foo", "foo");

        var before = DateTime.UtcNow;
        await service.RecordRequest("foo-backend", TestUser, request, inputs, TestWorkflow, CancellationToken.None);
        var after = DateTime.UtcNow;

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var records = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(records);

        var record = records.First();
        Assert.Equal("foo-backend", record.EntityName);
        Assert.Equivalent(TestUser, record.RequestedBy);
        Assert.InRange(record.RequestedAt, before.AddSeconds(-1), after.AddSeconds(1));
        
        var resources = record.Resources;
        
        Assert.NotNull(resources);
        Assert.Single(resources.S3Buckets);
        Assert.Equal("foo-backend", resources.S3Buckets[0].Service);
        Assert.Equal("my-test-bucket",resources.S3Buckets[0].Name);
        Assert.False(resources.S3Buckets[0].Versioning);
        Assert.Equal("dev", resources.S3Buckets[0].Environments);

        Assert.NotNull(record.Workflow);
        Assert.Equal(TestWorkflow, record.Workflow);
    }

    [Fact]
    public async Task Should_persist_multiple_resources_in_single_record()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var resources = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket { Service = "multi-svc", Name = "bucket-one", Environments = "dev" },
                new CreateTenantS3Bucket { Service = "multi-svc", Name = "bucket-two", Environments = "dev" }
            ]
        };
        var inputs = resources.ToWorkflowInputs("123", "foo", "foo");
        await service.RecordRequest("multi-svc", TestUser, resources, inputs, TestWorkflow, CancellationToken.None);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, record.Resources?.S3Buckets.Count);
        Assert.Equal(1, await collection.CountDocumentsAsync(
            Builders<ResourceRequestRecord>.Filter.Empty,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_persist_record_with_null_user_when_no_auth_token_provided()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request = new CreateTenantResourceRequest { S3Buckets = [
            new CreateTenantS3Bucket
            {
                Service = "anon-svc",
                Name = "anon-bucket",
                Environments = "test"
            }
        ] };
        
        var inputs = request.ToWorkflowInputs("123", "foo", "foo");
        await service.RecordRequest("anon-svc", null, request, inputs, TestWorkflow, CancellationToken.None);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal("anon-svc", record.EntityName);
        Assert.Null(record.RequestedBy);
    }

    [Fact]
    public async Task Should_attach_pull_request_to_existing_request_by_runid_and_branch()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket
                {
                    Service = "foo-backend",
                    Name = "my-test-bucket",
                    Environments = "dev"
                }
            ]
        };

        var inputs = request.ToWorkflowInputs("run-123", "tenant-request-run-123", "PR title");
        await service.RecordRequest("foo-backend", TestUser, request, inputs, TestWorkflow, CancellationToken.None);

        var updated = await service.AttachPullRequest(
            "run-123",
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/42",
                Number = 42
            },
            CancellationToken.None);

        Assert.True(updated);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(record.PullRequest);
        Assert.Equal("https://github.com/DEFRA/cdp-tenant-config/pull/42", record.PullRequest.Url);
        Assert.Equal(42, record.PullRequest.Number);
    }

    [Fact]
    public async Task Should_not_attach_pull_request_if_no_matching_request_exists()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket
                {
                    Service = "foo-backend",
                    Name = "my-test-bucket",
                    Environments = "dev"
                }
            ]
        };

        var inputs = request.ToWorkflowInputs("run-123", "tenant-request-run-123", "PR title");
        await service.RecordRequest("foo-backend", TestUser, request, inputs, TestWorkflow, CancellationToken.None);

        var updated = await service.AttachPullRequest(
            "other-run",
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/43",
                Number = 43
            },
            CancellationToken.None);

        Assert.False(updated);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(ResourceRequestService.CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Null(record.PullRequest);
    }
}