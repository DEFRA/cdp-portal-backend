using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create.Validators;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

public class SubscriptionValidatorTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{

    [Fact]
    public async Task Test_Subscribe_Validator_passes_with_no_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var validator = new CreateResourceValidator(mongoFactory);

        var entity1 = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Environments =
            {
                { "dev", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] } },
                { "test", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] } },
                { "perf-test", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] } },
                { "ext-test", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] } },
                { "prod", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] } },
            }
        };
        await entities.Create(entity1, ct);
        
        var entity2 = new Entity
        {
            Name = "foo-frontend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Frontend,
            Environments =
            {
                { "dev", new CdpTenant { SnsTopics = [new TenantSnsTopic { Name = "my-topic" }] } },
                { "test", new CdpTenant { SnsTopics = [new TenantSnsTopic { Name = "my-topic" }] } },
                { "perf-test", new CdpTenant { SnsTopics = [new TenantSnsTopic { Name = "my-topic" }] } },
                { "ext-test", new CdpTenant { SnsTopics = [new TenantSnsTopic { Name = "my-topic" }] } },
                { "prod", new CdpTenant { SnsTopics = [new TenantSnsTopic { Name = "my-topic" }] } },
            }
        };
        await entities.Create(entity2, ct);

        
        var req = new CreateTenantResourceRequest
        {
            Subscriptions = [new CreateTenantSubscription
            {
                Environments = "tenant",
                QueueService = "foo-backend",
                Queue = "my-queue",
                Topic = "my-topic",
                TopicService = "foo-frontend"
            }]
        };
        var errors = await validator.Validate(req, ct);

        Assert.Empty(errors);
    }
    
    [Fact]
    public async Task Test_Subscribe_Validator_passes_when_subscribing_to_new_resources()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var validator = new CreateResourceValidator(mongoFactory);

        var entity1 = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
        };
        await entities.Create(entity1, ct);
        
        var entity2 = new Entity
        {
            Name = "foo-frontend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Frontend,
        };
        await entities.Create(entity2, ct);

        
        var req = new CreateTenantResourceRequest
        {
            SnsTopics = [
                new CreateTenantSnsTopic
                {
                    Name = "my-topic",
                    Service = "foo-backend",
                    Environments = "tenant"
                }],
            SqsQueues = [
                new CreateTenantSqsQueue
                {
                    Name = "my-queue",
                    Service = "foo-frontend",
                    Environments = "tenant"
                }
            ],
            
            Subscriptions = [new CreateTenantSubscription
            {
                Environments = "tenant",
                QueueService = "foo-backend",
                Queue = "my-queue",
                Topic = "my-topic",
                TopicService = "foo-frontend"
            }]
        };
        var errors = await validator.Validate(req, ct);

        Assert.Empty(errors);
    }
    
    [Fact]
    public async Task Test_Subscribe_fails_when_queue_doesnt_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var validator = new CreateResourceValidator(mongoFactory);

        var entity1 = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend
        };
        await entities.Create(entity1, ct);
        
        var entity2 = new Entity
        {
            Name = "foo-frontend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Frontend
        };
        await entities.Create(entity2, ct);

        
        var req = new CreateTenantResourceRequest
        {
            Subscriptions = [new CreateTenantSubscription
            {
                Environments = "tenant",
                QueueService = "foo-backend",
                Queue = "my-queue",
                Topic = "my-topic",
                TopicService = "foo-frontend"
            }]
        };
        var errors = await validator.Validate(req, ct);

        Assert.Equal(2, errors.Count);
        Assert.Equal("SQS Subscription queue my-queue doesn't exist in dev", errors[0]);
        Assert.Equal("SQS Subscription topic my-topic doesn't exist in dev", errors[1]);
    }
}