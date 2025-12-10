using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents;

internal class MockHandler : IGithubWorkflowEventHandler
{
    public int CallCount { get; set; }
    public string EventType => "mock";
    public Task Handle(string message, CancellationToken cancellation)
    {
        CallCount += 1;
        return Task.CompletedTask;
    }
}

public class GitHubWorkflowEventListenerTest
{
    private readonly IAmazonSQS Sqs = Substitute.For<IAmazonSQS>();
    private readonly IOptions<GithubWorkflowEventListenerOptions> config =
        new OptionsWrapper<GithubWorkflowEventListenerOptions>(
            new GithubWorkflowEventListenerOptions { QueueUrl = "http://queue.url", Enabled = true });

    [Fact]
    public async Task TestMessagesAreDispatched()
    {
        var mockHandler = new MockHandler();
        var listener = new GithubWorkflowEventListener(Sqs, config, [mockHandler], NullLoggerFactory.Instance);
        
        var messageBody = """
                          {
                            "eventType": "mock",
                            "timestamp": "2024-10-23T15:10:10.123",
                            "payload": ""
                          }
                          """;
        await listener.Handle(new Message { MessageId = "1234", Body = messageBody }, CancellationToken.None);
        Assert.Equal(1, mockHandler.CallCount);
    }
    
    [Fact]
    public async Task TestUnknownMessagesAreNotDispatchedToWrongHandler()
    {
        var mockHandler = new MockHandler();
        var listener = new GithubWorkflowEventListener(Sqs, config, [mockHandler], NullLoggerFactory.Instance);
        
        var messageBody = """
                          {
                            "eventType": "wrong_type",
                            "timestamp": "2024-10-23T15:10:10.123",
                            "payload": ""
                          }
                          """;
        await listener.Handle(new Message { MessageId = "1234", Body = messageBody }, CancellationToken.None);
        Assert.Equal(0, mockHandler.CallCount);
    }
    
    [Fact]
    public async Task TestFailsIfDuplicateHandlersAreRegistered()
    {
        var mockHandler = new MockHandler();
        Assert.ThrowsAny<Exception>( () => new GithubWorkflowEventListener(Sqs, config, [mockHandler, mockHandler], NullLoggerFactory.Instance));
    }
}