using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create;

public class ResourceRequestServiceTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    private static readonly UserDetails TestUser = new() { Id = "user-123", DisplayName = "Test User" };
    private const string CollectionName = "resourceRequests";

    private readonly Team Team = new Team { Name = "Foo", TeamId = "foo" };
    private readonly Team TeamTwo = new Team { Name = "BAr", TeamId = "bar" };
    
    private static readonly GitHubTriggerWorkflowResponse TestWorkflow = new()
    {
        WorkflowRunId = 25552427454L,
        WorkflowRunUrl = "https://api.github.com/repos/DEFRA/cdp-tenant-config/actions/runs/25552427454",
        WorkflowRunHtmlUrl = "https://github.com/DEFRA/cdp-tenant-config/actions/runs/25552427454"
    };

    [Fact]
    public async Task Should_persist_resource_request_with_entity_user_and_workflow_link()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await service.RecordRequest(["foo-backend"], [Team], TestUser, request, inputs, TestWorkflow, ct);
        var after = DateTime.UtcNow;

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
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
        Assert.Equal(PrStatus.Pending, record.Status);

        Assert.NotNull(record.Workflow);
        Assert.Equal(TestWorkflow, record.Workflow);
    }

    [Fact]
    public async Task Should_persist_multiple_resources_in_single_record()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await service.RecordRequest(["multi-svc"], [Team], TestUser, resources, inputs, TestWorkflow, ct);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, record.Resources?.S3Buckets.Count);
        Assert.Equal(1, await collection.CountDocumentsAsync(
            Builders<ResourceRequestRecord>.Filter.Empty,
            cancellationToken: TestContext.Current.CancellationToken));
    }
    
    [Fact]
    public async Task Should_persist_all_services_in_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var resources = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket { Service = "foo-frontend", Name = "bucket-one", Environments = "dev" },
                new CreateTenantS3Bucket { Service = "foo-backend", Name = "bucket-two", Environments = "dev" }
            ]
        };
        var inputs = resources.ToWorkflowInputs("123", "foo", "foo");
        await service.RecordRequest(resources.GetServices(), [Team], TestUser, resources, inputs, TestWorkflow, ct);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, record.Resources?.S3Buckets.Count);
        Assert.Equivalent(new List<string> {"foo-frontend", "foo-backend"}, record.Entities);
    }

    [Fact]
    public async Task Should_persist_record_with_null_user_when_no_auth_token_provided()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await service.RecordRequest(["anon-svc"], [Team], null, request, inputs, TestWorkflow, ct);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal("anon-svc", record.EntityName);
        Assert.Null(record.RequestedBy);
    }

    [Fact]
    public async Task Should_attach_pull_request_to_existing_request_by_runid_and_branch()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await service.RecordRequest(["foo-backend"], [Team], TestUser, request, inputs, TestWorkflow, ct);

        var updated = await service.AttachPullRequest(
            "run-123",
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/42",
                Number = 42
            },
            ct);

        Assert.NotNull(updated);
        Assert.Equal("https://github.com/DEFRA/cdp-tenant-config/pull/42", updated.PullRequest?.Url);
        Assert.Equal(42, updated.PullRequest?.Number);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(record.PullRequest);
        Assert.Equal("https://github.com/DEFRA/cdp-tenant-config/pull/42", record.PullRequest.Url);
        Assert.Equal(42, record.PullRequest.Number);
        Assert.Equal(PrStatus.Requested, record.Status);
    }

    [Fact]
    public async Task Should_not_attach_pull_request_if_no_matching_request_exists()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await service.RecordRequest(["foo-backend"], [Team], TestUser, request, inputs, TestWorkflow, ct);

        var updated = await service.AttachPullRequest(
            "other-run",
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/43",
                Number = 43
            },
            ct);

        Assert.Null(updated);

        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Null(record.PullRequest);
    }
    
    [Fact]
    public async Task Should_update_pr_status()
    {
        var ct = TestContext.Current.CancellationToken;
        
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
        await service.RecordRequest(["foo-backend"], [Team], TestUser, request, inputs, TestWorkflow, ct);

        var updated = await service.AttachPullRequest(
            "run-123",
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/42",
                Number = 42
            },
            ct);

        Assert.NotNull(updated?.PullRequest);
        
        await service.UpdatePullRequestStatus(updated.PullRequest.Number, PrStatus.Merged, ct);
        
        var collection = mongoFactory.GetCollection<ResourceRequestRecord>(CollectionName);
        var record = await collection
            .Find(Builders<ResourceRequestRecord>.Filter.Empty)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(record.PullRequest);
        Assert.Equal(PrStatus.Merged, record.Status);
    }
    
    [Fact]
    public async Task Should_search_using_matcher()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new ResourceRequestService(mongoFactory, new NullLoggerFactory());

        var request1 = new CreateTenantResourceRequest();
        var inputs1 = request1.ToWorkflowInputs("run-1", "tenant-request-run-123", "PR title");
        await service.RecordRequest(["foo-backend", "foo-frontend"], [Team], TestUser, request1, inputs1, TestWorkflow, ct);
        
        var request2 = new CreateTenantResourceRequest();
        var inputs2 = request2.ToWorkflowInputs("run-2", "tenant-request-run-321", "PR title");
        await service.RecordRequest(["bar-backend"], [Team, TeamTwo], TestUser, request2, inputs2,
            TestWorkflow with { WorkflowRunId = 1243443 }, ct);
    

        await service.AttachPullRequest(
            inputs1.RunId!,
            new ResourceRequestPullRequest
            {
                Url = "https://github.com/DEFRA/cdp-tenant-config/pull/42",
                Number = 42
            },
            ct);

        var matches = await service.Find(new ResourceRequestMatcher(["foo-backend"], null, null, null, null), ct);
        Assert.Single(matches);

        matches = await service.Find(new ResourceRequestMatcher(["foo-frontend"], null, null, null, null), ct);
        Assert.Single(matches);
        
        matches = await service.Find(new ResourceRequestMatcher(["foo-frontend", "bar-backend"], null, null, null, null), ct);
        Assert.Equal(2, matches.Count);
        
        matches = await service.Find(new ResourceRequestMatcher([], null, null, null, null), ct);
        Assert.Equal(2, matches.Count);

        matches = await service.Find(new ResourceRequestMatcher(null, null, null, null, null), ct);
        Assert.Equal(2, matches.Count);

        matches = await service.Find(new ResourceRequestMatcher(null, null, ["requested"], null, null), ct);
        Assert.Single(matches);
        
        matches = await service.Find(new ResourceRequestMatcher(null, null, ["pending"], null, null), ct);
        Assert.Single(matches);
        
        matches = await service.Find(new ResourceRequestMatcher(null, null, null, TestUser.Id, null), ct);
        Assert.Equal(2, matches.Count);
        
        matches = await service.Find(new ResourceRequestMatcher(null, [Team.TeamId!], null, TestUser.Id, null), ct);
        Assert.Equal(2, matches.Count);
        
        matches = await service.Find(new ResourceRequestMatcher(null, [TeamTwo.TeamId!], null, TestUser.Id, null), ct);
        Assert.Single(matches);

        matches = await service.Find(new ResourceRequestMatcher(null, [], null, TestUser.Id, null), ct);
        Assert.Equal(2, matches.Count);

        matches = await service.Find(new ResourceRequestMatcher(null, [], null, TestUser.Id, null), ct);
        Assert.Equal(2, matches.Count);

        matches = await service.Find(new ResourceRequestMatcher(null, [], null, null, DateTime.Now.AddHours(-1)), ct);
        Assert.Equal(2, matches.Count);

        matches = await service.Find(new ResourceRequestMatcher(null, [], null, null, DateTime.Now.AddHours(1)), ct);
        Assert.Empty(matches);
    }
}