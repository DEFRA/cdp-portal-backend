using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using File = System.IO.File;
using JsonSerializer = System.Text.Json.JsonSerializer;
using User = Defra.Cdp.Backend.Api.Utils.Clients.User;

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


    [Fact]
    public async Task TestLatestForService()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "CodeBuildStateChangeHandler");
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());

        List<DatabaseMigration> migrations =
        [
            new DatabaseMigration
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new User(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-5)
            },

            new DatabaseMigration
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new User(),
                Version = "0.5.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now
            },
            new DatabaseMigration
            {
                Environment = "dev",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new User(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-4)
            },
            new DatabaseMigration
            {
                Environment = "dev",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new User(),
                Version = "0.5.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-1)
            },
            new DatabaseMigration
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "another-backend",
                User = new User(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now
            }
        ];

        foreach (var databaseMigration in migrations)
        {
            await service.CreateMigration(databaseMigration, CancellationToken.None);
        }


        var result = await service.LatestForService("test-backend", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Environment == "dev" && m.Version == "0.5.0");
        Assert.Contains(result, m => m.Environment == "test" && m.Version == "0.5.0");
    }
}