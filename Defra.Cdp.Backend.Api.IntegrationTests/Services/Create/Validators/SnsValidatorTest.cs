using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create.Validators;

public class SnsValidatorTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task Test_Sns_Validator_passes_with_no_errors()
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
                    "dev", new CdpTenant { SnsTopics = [ new TenantSnsTopic {Name = "mytopic" } ]}
                }
            }
        };
        await entities.Create(entity, ct);
        var req = new CreateTenantResourceRequest {
                SnsTopics = [new CreateTenantSnsTopic { Name = "foobar", Environments = "tenant", Service = "foo-backend" }],
        };
        var errors = await validator.Validate(req,  ct);
        
        Assert.Empty(errors);
    }
    
    [Fact]
    public async Task Test_Sns_Validator_fails_with_invalid_service()
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
                    "dev", new CdpTenant { SnsTopics = [ new TenantSnsTopic {Name = "mytopic" } ]}
                }
            }
        };
        await entities.Create(entity, ct);
        var req = new CreateTenantResourceRequest {
            SnsTopics = [new CreateTenantSnsTopic { Name = "foo", Environments = "tenant", Service = "i-dont-exist" }],
        };
        var errors = await validator.Validate(req,  ct);

        Assert.Single(errors);
        Assert.Equal("SNS Topic foo is assigned to an unknown service: i-dont-exist", errors[0]);
    }
    
    [Fact]
    public async Task Test_Sns_Validator_fails_with_topic_exists()
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
                    "dev", new CdpTenant { SnsTopics = [ new TenantSnsTopic {Name = "mytopic" } ]}
                }
            }
        };
        await entities.Create(entity, ct);

        var req = new CreateTenantResourceRequest
        {
            SnsTopics = [new CreateTenantSnsTopic { Name = "mytopic", Environments = "tenant", Service = "foo-backend" }]
        };
        var errors = await validator.Validate(req,  ct);

        Assert.Single(errors);
        Assert.Equal("SNS Topic mytopic already exists for service foo-backend", errors[0]);
    }
}