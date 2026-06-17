using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Create.Validators;

public class S3ValidatorTest(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task Test_empty_request_fails_with_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var entityResourceService = new EntityResourceService(mongoFactory);
        var validator = new CreateResourceValidator(entityResourceService);

        var errors = await validator.Validate(new CreateTenantResourceRequest(),  ct);
        Assert.Single(errors);
        Assert.Equal("The request has no resources", errors[0]);
    }
    
    [Fact]
    public async Task Test_S3_Validator_passes_with_no_errors()
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
                    "dev", new CdpTenant { S3Buckets = [new TenantS3Bucket { BucketName = "dev-baz-c63f2" }] }
                }
            }
        };
        await entities.Create(entity, ct);
        
        var errors = await validator.Validate(new CreateTenantResourceRequest { S3Buckets = [new CreateTenantS3Bucket { Name = "foobar", Environments = "tenants", Service = "foo-backend" }] },  ct);
        Assert.Empty(errors);
    }
    
    [Fact]
    public async Task Test_S3_Validator_bucket_exists()
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
                    "dev", new CdpTenant { S3Buckets = [new TenantS3Bucket { BucketName = "dev-foobar-c63f2" }] }
                }
            }
        };
        await entities.Create(entity, ct);
        var errors = await validator.Validate(new CreateTenantResourceRequest { S3Buckets = [new CreateTenantS3Bucket { Name = "foobar", Environments = "tenants", Service = "foo-backend" }] }, ct);
        
        Assert.Single(errors);
        Assert.Equal("S3 Bucket foobar already exists for service foo-backend", errors[0]);
    }
    
    [Fact]
    public async Task Test_S3_Validator_invalid_service()
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
            SubType = SubType.Backend
        };
        await entities.Create(entity, ct);
        var errors = await validator.Validate(new CreateTenantResourceRequest { S3Buckets = [new CreateTenantS3Bucket { Name = "foobar", Environments = "tenants", Service = "i-dont-exist" }] },  ct);
        
        Assert.Single(errors);
        
        Assert.Equal("S3 Bucket foobar is assigned to an unknown service: i-dont-exist", errors[0]);
    }
    
    [Fact]
    public async Task Test_S3_Validator_invalid_name()
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
            SubType = SubType.Backend
        };
        await entities.Create(entity, ct);

        var longName =
            "abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd123456abcd12345a";
        var errors = await validator.Validate(new CreateTenantResourceRequest { S3Buckets = [new CreateTenantS3Bucket { Name = longName, Environments = "tenants", Service = "foo-backend" }]},  ct);
        
        Assert.Single(errors);
        Assert.Equal($"S3 Bucket {longName} name is too long (max 46 chars)", errors[0]);
    }
}