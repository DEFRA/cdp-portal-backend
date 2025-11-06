using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;

using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using File = System.IO.File;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Aws.Deployments;

public class CodeBuildStateChangeHandlerTest(MongoContainerFixture fixture) : ServiceTest(fixture)
{
    private const string AwsAccount = "0000000000";

    [Fact]
    public async Task TestCreateAndLinkingMessages()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());
        var handler = new CodeBuildStateChangeHandler(service, new NullLogger<CodeBuildStateChangeHandler>());

        const string buildId = "arn:aws:codebuild:eu-west-2:0000000000:build/kurne-test-liquibase:b93ef1d9-47fa-4a91-b8cf-902987cd9fbc";
        const string cdpMigrationId = "cdp-migration-0000";

        await service.CreateMigration(new DatabaseMigration
        {
            Environment = "test",
            CdpMigrationId = cdpMigrationId,
            Service = "test-backend",
            User = new UserDetails(),
            Version = "0.1.0"
        }, TestContext.Current.CancellationToken);



        var lambdaEvent = new CodeBuildLambdaEvent(
            CdpMigrationId: cdpMigrationId,
            BuildId: buildId,
            Account: AwsAccount,
            Time: DateTime.Now
        );
        await handler.Handle("id", lambdaEvent, TestContext.Current.CancellationToken);

        var result = await service.FindByBuildId(buildId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(cdpMigrationId, result.CdpMigrationId);
        Assert.Equal(buildId, result.BuildId);
    }

    [Fact]
    public async Task CreatingOnLinkFailure()
    {
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());
        var handler = new CodeBuildStateChangeHandler(service, new NullLogger<CodeBuildStateChangeHandler>());

        const string buildId = "arn:aws:codebuild:eu-west-2:0000000000:build/kurne-test-liquibase:43245435";
        const string cdpMigrationId = "cdp-43545511";

        var lambdaEvent = new CodeBuildLambdaEvent(
            CdpMigrationId: cdpMigrationId,
            BuildId: buildId,
            Account: AwsAccount,
            Time: DateTime.Now,
            Request: new DatabaseMigrationRequest
            {
                Environment = "test",
                CdpMigrationId = cdpMigrationId,
                Service = "foo",
                User = new UserDetails
                {
                    Id = "1234",
                    DisplayName = "test user"
                },
                Version = "0.1.0"
            }
        );
        await handler.Handle("id", lambdaEvent, TestContext.Current.CancellationToken);

        var result = await service.FindByBuildId(buildId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(cdpMigrationId, result.CdpMigrationId);
        Assert.Equal(buildId, result.BuildId);
    }

    [Fact]
    public async Task TestUpdateMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());
        var handler = new CodeBuildStateChangeHandler(service, new NullLogger<CodeBuildStateChangeHandler>());

        const string cdpMigrationId = "cdp-migration-0001";
        const string buildId =
            "arn:aws:codebuild:eu-west-2:000000000000:build/kurne-test-liquibase:d5ac2e30-dd0d-494f-a57d-515726439d85";


        var inProgressEvent = JsonSerializer.Deserialize<CodeBuildStateChangeEvent>(File.ReadAllText("Resources/codebuild/in-progress.json"));
        Assert.NotNull(inProgressEvent);

        var succeededEvent = JsonSerializer.Deserialize<CodeBuildStateChangeEvent>(File.ReadAllText("Resources/codebuild/succeeded.json"));
        Assert.NotNull(succeededEvent);

        await service.CreateMigration(new DatabaseMigration
        {
            Environment = "test",
            CdpMigrationId = cdpMigrationId,
            Service = "test-backend",
            User = new UserDetails(),
            Version = "0.1.0"
        }, cancellationToken);

        await service.Link(cdpMigrationId, buildId, cancellationToken);

        await handler.Handle("1", inProgressEvent, cancellationToken);

        var build = await service.FindByBuildId(buildId, cancellationToken);
        Assert.NotNull(build);
        Assert.Equal("IN_PROGRESS", build.Status);

        await handler.Handle("2", succeededEvent, cancellationToken);
        build = await service.FindByBuildId(buildId, cancellationToken);
        Assert.NotNull(build);
        Assert.Equal("SUCCEEDED", build.Status);
    }


    [Fact]
    public async Task TestLatestForService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mongoFactory = CreateMongoDbClientFactory();
        var service = new DatabaseMigrationService(mongoFactory, new NullLoggerFactory());

        List<DatabaseMigration> migrations =
        [
            new()
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new UserDetails(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-5)
            },

            new()
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new UserDetails(),
                Version = "0.5.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now
            },
            new()
            {
                Environment = "dev",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new UserDetails(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-4)
            },
            new()
            {
                Environment = "dev",
                CdpMigrationId = "1",
                Service = "test-backend",
                User = new UserDetails(),
                Version = "0.5.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now.AddDays(-1)
            },
            new()
            {
                Environment = "test",
                CdpMigrationId = "1",
                Service = "another-backend",
                User = new UserDetails(),
                Version = "0.4.0",
                Status = CodeBuildStatuses.Succeeded,
                Updated = DateTime.Now
            }
        ];

        foreach (var databaseMigration in migrations)
        {
            await service.CreateMigration(databaseMigration, cancellationToken);
        }


        var result = await service.LatestForService("test-backend", cancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Environment == "dev" && m.Version == "0.5.0");
        Assert.Contains(result, m => m.Environment == "test" && m.Version == "0.5.0");
    }
}