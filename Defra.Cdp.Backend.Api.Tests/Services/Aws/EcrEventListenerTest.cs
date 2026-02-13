using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Dependencies;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws;

public class EcrEventListenerTest
{
    private static readonly ILogger<EcrEventListener> s_logger = NullLogger<EcrEventListener>.Instance;
    private readonly IDeployableArtifactsService _artifacts = Substitute.For<IDeployableArtifactsService>();
    private readonly IAutoDeploymentTriggerExecutor _autoDeploymentTriggerExecutor = Substitute.For<IAutoDeploymentTriggerExecutor>();
    private readonly ISbomEcrEventHandler _sbomEventHandler = Substitute.For<ISbomEcrEventHandler>();
    
    [Fact]
    public async Task TestInvalidMessage()
    {
        var handler = new EcrEventHandler(_artifacts, _autoDeploymentTriggerExecutor, _sbomEventHandler, s_logger);
        Task Act() => handler.Handle("123", "invalid", CancellationToken.None);
        await Assert.ThrowsAsync<JsonException>(Act);

        await _artifacts.DidNotReceive()
            .RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _autoDeploymentTriggerExecutor.DidNotReceive()
            .Handle(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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

        var handler = new EcrEventHandler(_artifacts, _autoDeploymentTriggerExecutor, _sbomEventHandler, s_logger);
        await handler.Handle("123", message, CancellationToken.None);

        await _artifacts
            .Received()
            .CreateAsync(
                Arg.Is<DeployableArtifact>(d => d.Repo ==  "my-repository-name" && d.Tag == "0.1.0" && d.Sha256 == "sha256:7f5b2640fe6fb4f46592dfd3410c4a79dac4f89e4782432e0378abcd1234"), Arg.Any<CancellationToken>());
        await _artifacts.DidNotReceive()
            .RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _autoDeploymentTriggerExecutor.Received()
            .Handle(Arg.Is("my-repository-name"), Arg.Is("0.1.0"), Arg.Any<CancellationToken>());
        await _sbomEventHandler.Received().Handle(Arg.Any<CancellationToken>());
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
        var handler = new EcrEventHandler(_artifacts, _autoDeploymentTriggerExecutor, _sbomEventHandler, s_logger);
        await handler.Handle("123", message, CancellationToken.None);
       
        await _artifacts.DidNotReceive()
            .CreateAsync(Arg.Any<DeployableArtifact>(), Arg.Any<CancellationToken>());
        await _artifacts.DidNotReceive()
            .RemoveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _autoDeploymentTriggerExecutor.DidNotReceive()
            .Handle(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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

        var handler = new EcrEventHandler(_artifacts, _autoDeploymentTriggerExecutor, _sbomEventHandler, s_logger);
        await handler.Handle("123", message, CancellationToken.None);
        
        await _artifacts.DidNotReceive()
            .CreateAsync(Arg.Any<DeployableArtifact>(), Arg.Any<CancellationToken>());
        await _artifacts.Received()
            .RemoveAsync(Arg.Is("my-repository-name"), Arg.Is("0.1.0"), Arg.Any<CancellationToken>());
        await _autoDeploymentTriggerExecutor.DidNotReceive()
            .Handle(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}