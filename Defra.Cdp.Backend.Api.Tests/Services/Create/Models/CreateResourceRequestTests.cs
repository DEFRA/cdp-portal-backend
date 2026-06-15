using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Create.Models;

public class CreateResourceRequestTests
{

    [Fact]
    public void Test_GetServices_gets_unique_services()
    {
        var request = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket
                {
                    Service = "foo-backend", Name = "my-bucket", Environments = "tenant", Versioning = false
                },
                new CreateTenantS3Bucket
                {
                    Service = "foo-frontend", Name = "another-bucket", Environments = "tenant", Versioning = false
                },
            ],
            SnsTopics = [
                new CreateTenantSnsTopic
                {
                    Service = "bar-backend", Name = "my-topic", Environments = "tenant"
                },
                new CreateTenantSnsTopic
                {
                    Service = "bar-frontend", Name = "another-topic", Environments = "tenant"
                }
            ],
            SqsQueues = [
                new CreateTenantSqsQueue
                {
                    Service = "foo-frontend", Name = "my-queue", Environments = "tenant"
                },
                new CreateTenantSqsQueue
                {
                    Service = "bar-backend", Name = "another-queue", Environments = "tenant"
                }
            ]
        };

        var services = request.GetServices();
        services.Sort();

        Assert.Equivalent( new List<string> {
            "bar-backend", "bar-frontend", "foo-backend", "foo-frontend"
        }, services);
    }
    
    [Fact]
    public void Test_ToWorkflowInputs()
    {
        var request = new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket
                {
                    Service = "foo-backend", Name = "my-bucket", Environments = "tenant", Versioning = false
                },
                new CreateTenantS3Bucket
                {
                    Service = "foo-frontend", Name = "another-bucket", Environments = "tenant", Versioning = false
                },
            ],
            SnsTopics = [
                new CreateTenantSnsTopic
                {
                    Service = "bar-backend", Name = "my-topic", Environments = "tenant"
                },
                new CreateTenantSnsTopic
                {
                    Service = "bar-frontend", Name = "another-topic", Environments = "tenant"
                }
            ],
            SqsQueues = [
                new CreateTenantSqsQueue
                {
                    Service = "foo-frontend", Name = "my-queue", Environments = "tenant"
                },
                new CreateTenantSqsQueue
                {
                    Service = "bar-backend", Name = "another-queue", Environments = "tenant"
                }
            ]
        };

        var inputs = request.ToWorkflowInputs("1234", "foo", "pr");
        const string expected = """["tenant s3-buckets add --service-name foo-backend --bucket-name my-bucket --environment tenant","tenant s3-buckets add --service-name foo-frontend --bucket-name another-bucket --environment tenant","tenant sqs-queues add --service-name foo-frontend --queue-names my-queue --environment tenant","tenant sqs-queues add --service-name bar-backend --queue-names another-queue --environment tenant","tenant sns-topics add --service-name bar-backend --topic-names my-topic --environment tenant","tenant sns-topics add --service-name bar-frontend --topic-names another-topic --environment tenant"]""";
        Assert.Equivalent(expected, inputs.Commands);
    }
    
    [Fact]
    public void Test_CreateTenantS3Bucket_generate_correct_cmd()
    {
        var request1 = new CreateTenantS3Bucket
        {
            Service = "foo-backend", Name = "my-bucket", Environments = "tenant", Versioning = false
        };
        
        var request2 = new CreateTenantS3Bucket
        {
            Service = "foo-backend", Name = "my-bucket", Environments = "tenant", Versioning = true
        };
        
        // These must match the params accepted by the cdp-cli from cdp-tenant-config
        Assert.Equal("tenant s3-buckets add --service-name foo-backend --bucket-name my-bucket --environment tenant", request1.ToWorkflowCommand());
        Assert.Equal("tenant s3-buckets add --service-name foo-backend --bucket-name my-bucket --environment tenant --versioning", request2.ToWorkflowCommand());
    }
    
    [Fact]
    public void Test_CreateTenantSqsQueue_generate_correct_cmd()
    {
        var request1 = new CreateTenantSqsQueue
        {
            Service = "foo-backend", Name = "my-queue", Environments = "tenant"
        };
        
        var request2 = new CreateTenantSqsQueue
        {
            Service = "foo-backend", Name = "my-queue", Environments = "tenant", VisibilityTimeout = 200, Fifo = true,
            DqlMaxReceiveCount = 3, DeduplicationScope = "messageGroup", FifoThroughputLimit = "perMessageGroupId", 
            RedriveAllowPolicyByQueue = true
        };

        
        // These must match the params accepted by the cdp-cli from cdp-tenant-config
        Assert.Equal("tenant sqs-queues add --service-name foo-backend --queue-names my-queue --environment tenant", request1.ToWorkflowCommand());
        Assert.Equal("tenant sqs-queues add --service-name foo-backend --queue-names my-queue --environment tenant --queue-type fifo --visibility-timeout 200 --fifo-throughput-limit perMessageGroupId --dlq-max-receive-count 3 --deduplication-scope messageGroup --redrive-allow-policy-by-queue", request2.ToWorkflowCommand());
    }
    
    [Fact]
    public void Test_CreateTenantSnsTopics_generate_correct_cmd()
    {
        var request1 = new CreateTenantSnsTopic
        {
            Service = "foo-backend", Name = "my-topic", Environments = "tenant"
        };
        
        var request2 = new CreateTenantSnsTopic
        {
            Service = "foo-backend", Name = "my-topic", Environments = "tenant", 
            Fifo = true, ContentDeduplication = true
        };

        
        // These must match the params accepted by the cdp-cli from cdp-tenant-config
        Assert.Equal("tenant sns-topics add --service-name foo-backend --topic-names my-topic --environment tenant", request1.ToWorkflowCommand());
        Assert.Equal("tenant sns-topics add --service-name foo-backend --topic-names my-topic --environment tenant --topic-type fifo --content-based-deduplication", request2.ToWorkflowCommand());
    }
    
    [Fact]
    public void Test_CreateTenantSubscription_generate_correct_cmd()
    {
        var request1 = new CreateTenantSubscription
        {
            QueueService = "foo-backend", 
            Queue = "my-queue",
            TopicService = "foo-admin",
            Topic = "my-topic",
            Environments = "tenant"
        };
        
        // These must match the params accepted by the cdp-cli from cdp-tenant-config
        Assert.Equal("tenant sqs-queues subscriptions add --environment tenant --service foo-backend --queue-name my-queue --topic-full-name my-topic", request1.ToWorkflowCommand([]));
    }
    
    [Fact]
    public void Test_CreateTenantSubscription_uses_fifo_queues_from_request()
    {
        var request1 = new CreateTenantSubscription
        {
            QueueService = "foo-backend", 
            Queue = "my-queue",
            TopicService = "foo-admin",
            Topic = "my-topic",
            Environments = "tenant"
        };
        List<CreateTenantSnsTopic> topics =
        [
            new() { Name = "my-topic", Fifo = true, Service = "foo-admin", Environments = "tenant" }
        ];
        
        // These must match the params accepted by the cdp-cli from cdp-tenant-config
        Assert.Equal("tenant sqs-queues subscriptions add --environment tenant --service foo-backend --queue-name my-queue --topic-full-name my-topic.fifo", request1.ToWorkflowCommand(topics));
    }
    
    [Fact]
    public void Test_deserialize_create_tenant_resource_request()
    {
        var json = """
                   {
                   "s3_buckets": [{
                     "service": "cdp-portal-backend",
                     "name": "testing123",
                     "versioning": true,
                     "environments": "dev"
                   }],
                   "sqs_queues": [],
                   "sns_topics": []
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<CreateTenantResourceRequest>(json);
        Assert.NotNull(result);
    }
    
    [Fact]
    public void Test_deserialize_github_response()
    {
        var json = """
                   {
                   "workflow_run_id": 25552427454,
                   "run_url": "https://api.github.com/repos/DEFRA/cdp-tenant-config/actions/runs/25552427454",
                   "html_url": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/25552427454"
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<GitHubTriggerWorkflowResponse>(json);
        Assert.NotNull(result);
    }
}