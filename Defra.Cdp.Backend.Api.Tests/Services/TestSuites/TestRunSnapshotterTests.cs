using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.TestSuites;

public class TestRunSnapshotterTests
{
    private readonly IOptions<MonoLambdaOptions> _config =
        new OptionsWrapper<MonoLambdaOptions>(
            new MonoLambdaOptions { QueueUrl = "http://queue.url", Enabled = true, TopicArn = "arn:aws:sns:region:account-id:topic-name"});

    [Fact]
    public async Task Test_snapshotter_triggers_with_correct_payload()
    {
        
        var mockSns = Substitute.For<IAmazonSimpleNotificationService>();
        var snapshotter =
            new TestRunSnapshotter(new MonoLambdaTrigger(mockSns, _config, NullLogger<MonoLambdaTrigger>.Instance));
        mockSns.PublishAsync(Arg.Any<string>(), Arg.Is(""), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(new PublishResponse { HttpStatusCode = HttpStatusCode.OK });
        
        var run = new TestRun
        {
            RunId = "1234",
            Created = new DateTime(2026, 2, 1, 12, 30, 1),
            TaskLastUpdate = new DateTime(2026, 2, 1, 12, 35, 1),
            Environment = "dev"
        };
        await snapshotter.Snapshot(run, TestContext.Current.CancellationToken);

        await mockSns.Received().PublishAsync(Arg.Is<PublishRequest>(p => p.MessageGroupId == run.Environment && p.Message.Contains("grafana_snapshots")), Arg.Any<CancellationToken>());
    }
}