using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create.Validators;

public class SqsValidatorTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    
    [Fact]
    public async Task Test_Sqs_Validator_passes_with_no_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var entityResourceService = new EntityResourceService(mongoFactory);
        var validator = new CreateResourceValidator(entityResourceService);

        var entity = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Environments =
            {
                {
                    "dev", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] }
                }
            }
        };
        await entities.Create(entity, ct);
        var req = new CreateTenantResourceRequest
        {
            SqsQueues = [new CreateTenantSqsQueue() { Name = "foobar", Environments = "tenants", Service = "foo-backend" }]
        };
        var errors = await validator.Validate(req,  ct);
        
        Assert.Empty(errors);
    }
    
    [Fact]
    public async Task Test_Sqs_Validator_fails_with_invalid_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var entityResourceService = new EntityResourceService(mongoFactory);
        var validator = new CreateResourceValidator(entityResourceService);

        var entity = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Environments =
            {
                {
                    "dev", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] }
                }
            }
        };
        await entities.Create(entity, ct);
        
        var req = new CreateTenantResourceRequest
        {
            SqsQueues = [new CreateTenantSqsQueue { Name = "foo", Environments = "tenants", Service = "i-dont-exist" }]
        };
        var errors = await validator.Validate(req,  ct);

        Assert.Single(errors);
        Assert.Equal("SQS Queue foo is assigned to an unknown service: i-dont-exist", errors[0]);
    }
    
    [Fact]
    public async Task Test_Sqs_Validator_fails_with_invalid_environment()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var entityResourceService = new EntityResourceService(mongoFactory);
        var validator = new CreateResourceValidator(entityResourceService);

        var entity = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Environments = { { "dev", new CdpTenant() } }
        };
        await entities.Create(entity, ct);
        
        var req = new CreateTenantResourceRequest
        {
            SqsQueues = [new CreateTenantSqsQueue { Name = "foo", Environments = "pre-prod", Service = "foo-backend" }]
        };
        var errors = await validator.Validate(req,  ct);

        Assert.Single(errors);
        Assert.Equal("SQS Queue foo has an invalid or missing environment: pre-prod", errors[0]);
    }
    
    [Fact]
    public async Task Test_Sqs_Validator_fails_with_queue_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entities = new EntitiesService(mongoFactory, new NullLoggerFactory());
        var entityResourceService = new EntityResourceService(mongoFactory);
        var validator = new CreateResourceValidator(entityResourceService);

        var entity = new Entity
        {
            Name = "foo-backend",
            Status = Status.Created,
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Environments =
            {
                {
                    "dev", new CdpTenant { SqsQueues = [new TenantSqsQueue { Name = "my-queue" }] }
                }
            }
        };
        await entities.Create(entity, ct);

        await entities.Create(new Entity { Name = "foo-admin", Environments = { { "dev", new CdpTenant() }}}, ct);
        
        var req = new CreateTenantResourceRequest
        {
            SqsQueues = [new CreateTenantSqsQueue { Name = "my-queue", Environments = "tenants", Service = "foo-admin" }]
        };
        var errors = await validator.Validate(req,  ct);

        Assert.Single(errors);
        Assert.Equal("SQS Queue my-queue already exists for service foo-backend", errors[0]);
    }
}