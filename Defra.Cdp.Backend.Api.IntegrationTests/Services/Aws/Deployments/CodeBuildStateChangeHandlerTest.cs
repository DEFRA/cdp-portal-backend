using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Aws.Deployments;

public class CodeBuildStateChangeHandlerTest(MongoIntegrationTest fixture)  : ServiceTest(fixture)
{
    private const string AwsAccount = "0000000000";
    
    
    [Fact]
    public async Task TestCreateAndLinkingMessages()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "CodeBuildStateChangeHandler");
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());
        var handler = new CodeBuildStateChangeHandler(service, new NullLogger<CodeBuildStateChangeHandler>());

        const string buildId = "arn:aws:codebuild:eu-west-2:0000000000:build/kurne-test-liquibase:b93ef1d9-47fa-4a91-b8cf-902987cd9fbc";
        const string cdpMigrationId = "cdp-migration-0000";
        
        await service.CreateMigration(new DatabaseMigration
        {
            Environment = "test",
            CdpMigrationId = cdpMigrationId,
            Service = "test-backend",
            User = new User(),
            Version = "0.1.0",
        }, CancellationToken.None);
        
        var lambdaEvent = new CodeBuildLambdaEvent(
            CdpMigrationId: cdpMigrationId,
            BuildId: buildId,
            Account: AwsAccount,
            Time: DateTime.Now
        );
        await handler.Handle("id", lambdaEvent, CancellationToken.None);

        var result = await service.FindByBuildId(buildId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(cdpMigrationId, result.CdpMigrationId);
        Assert.Equal(buildId, result.BuildId);
    }
    
    [Fact]
    public async Task TestUpdateMessages()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "CodeBuildStateChangeHandler");
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());
        var handler = new CodeBuildStateChangeHandler(service, new NullLogger<CodeBuildStateChangeHandler>());

        const string cdpMigrationId = "cdp-migration-0001";
        const string buildId =
            "arn:aws:codebuild:eu-west-2:000000000000:build/kurne-test-liquibase:d5ac2e30-dd0d-494f-a57d-515726439d85";

        
        var inProgressEvent =  JsonSerializer.Deserialize<CodeBuildStateChangeEvent>(File.ReadAllText("Resources/codebuild/in-progress.json"));
        Assert.NotNull(inProgressEvent);

        var succeededEvent = JsonSerializer.Deserialize<CodeBuildStateChangeEvent>(File.ReadAllText("Resources/codebuild/succeeded.json"));
        Assert.NotNull(succeededEvent);
        
        await service.CreateMigration(new DatabaseMigration
        {
            Environment = "test",
            CdpMigrationId = cdpMigrationId,
            Service = "test-backend",
            User = new User(),
            Version = "0.1.0",
        }, CancellationToken.None);

        await service.Link(cdpMigrationId, buildId, CancellationToken.None);

        await handler.Handle("1", inProgressEvent, CancellationToken.None);

        var build = await service.FindByBuildId(buildId, CancellationToken.None);
        Assert.NotNull(build);
        Assert.Equal("IN_PROGRESS", build.Status);
        
        await handler.Handle("2", succeededEvent, CancellationToken.None);
        build = await service.FindByBuildId(buildId, CancellationToken.None);
        Assert.NotNull(build);
        Assert.Equal("SUCCEEDED", build.Status);

    }
}