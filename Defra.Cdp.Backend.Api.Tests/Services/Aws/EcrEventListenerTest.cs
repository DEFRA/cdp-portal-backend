using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws;

public class EcrEventListenerTest
{
    
    IArtifactScanner docker = Substitute.For<IArtifactScanner>();
    IDeployableArtifactsService artifacts = Substitute.For<IDeployableArtifactsService>();
    ILogger<EcrEventListener> logger = ConsoleLogger.CreateLogger<EcrEventListener>();
    
    [Fact]
    public async Task TestInvalidMessage()
    {
        var handler = new EcrMessageHandler(docker, artifacts, logger);
        var act = () => handler.Handle("123", "invalid", new CancellationToken());

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(act);
        await docker.DidNotReceive().ScanImage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await artifacts.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task TestValidPushMessage()
    {
        var message = """
                      {
                          "version": "0",
                          "id": "13cde686-328b-6117-af20-0e5566167482",
                          "detail-type": "ECR Image Action",
                          "source": "aws.ecr",
                          "account": "123456789012",
                          "time": "2019-11-16T01:54:34Z",
                          "region": "us-west-2",
                          "resources": [],
                          "detail": {
                              "result": "SUCCESS",
                              "repository-name": "my-repository-name",
                              "image-digest": "sha256:7f5b2640fe6fb4f46592dfd3410c4a79dac4f89e4782432e0378abcd1234",
                              "action-type": "PUSH",
                              "image-tag": "0.1.0"
                          }
                      }
                      """;
        docker.ScanImage("my-repository-name", "0.1.0", Arg.Any<CancellationToken>()).Returns(new ArtifactScannerResult(new DeployableArtifact()
        {
            Repo = "my-repository-name",
            Tag = "0.1.0",
            Sha256 = "sha256:7f5b2640fe6fb4f46592dfd3410c4a79dac4f89e4782432e0378abcd1234"
        }));
        var handler = new EcrMessageHandler(docker, artifacts, logger);
        await handler.Handle("123", message, new CancellationToken());
        await docker.Received().ScanImage(Arg.Is("my-repository-name"), Arg.Is("0.1.0"), Arg.Any<CancellationToken>());
        await artifacts.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestNonSemverPushMessage()
    {
        var message = """
                      {
                          "version": "0",
                          "id": "13cde686-328b-6117-af20-0e5566167482",
                          "detail-type": "ECR Image Action",
                          "source": "aws.ecr",
                          "account": "123456789012",
                          "time": "2019-11-16T01:54:34Z",
                          "region": "us-west-2",
                          "resources": [],
                          "detail": {
                              "result": "SUCCESS",
                              "repository-name": "my-repository-name",
                              "image-digest": "sha256:7f5b2640fe6fb4f46592dfd3410c4a79dac4f89e4782432e0378abcd1234",
                              "action-type": "PUSH",
                              "image-tag": "27"
                          }
                      }
                      """;
        var handler = new EcrMessageHandler(docker, artifacts, logger);
        await handler.Handle("123", message, new CancellationToken());
        await docker.DidNotReceive().ScanImage(Arg.Is("my-repository-name"), Arg.Is("0.1.0"), Arg.Any<CancellationToken>());
        await artifacts.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    
    [Fact]
    public async Task TestValidDeleteMessage()
    {
        var message = """
                      {
                          "version": "0",
                          "id": "13cde686-328b-6117-af20-0e5566167482",
                          "detail-type": "ECR Image Action",
                          "source": "aws.ecr",
                          "account": "123456789012",
                          "time": "2019-11-16T01:54:34Z",
                          "region": "us-west-2",
                          "resources": [],
                          "detail": {
                              "result": "SUCCESS",
                              "repository-name": "my-repository-name",
                              "image-digest": "sha256:7f5b2640fe6fb4f46592dfd3410c4a79dac4f89e4782432e0378abcd1234",
                              "action-type": "DELETE",
                              "image-tag": "0.1.0"
                          }
                      }
                      """;
        
        var handler = new EcrMessageHandler(docker, artifacts, logger);
        await handler.Handle("123", message, new CancellationToken());
        await docker.DidNotReceive().ScanImage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await artifacts.Received().RemoveAsync(Arg.Is("my-repository-name"), Arg.Is("0.1.0"),  Arg.Any<CancellationToken>());
    }
}