using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.EventHistory;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambdaEvents;

internal class MockHandler : IMonoLambdaEventHandler
{
    public int CallCount { get; set; }
    public string EventType => "mock";
    public bool PersistEvents => false;
    public Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        CallCount += 1;
        return Task.CompletedTask;
    }
}


public class MonoLambdaEventListenerTest
{
    private readonly IEventHistoryFactory _eventHistoryFactory = Substitute.For<IEventHistoryFactory>();
    private readonly IAmazonSQS Sqs = Substitute.For<IAmazonSQS>();

    private readonly IOptions<LambdaEventListenerOptions> config =
        new OptionsWrapper<LambdaEventListenerOptions>(
            new LambdaEventListenerOptions { QueueUrl = "http://queue.url", Enabled = true });

    
    [Fact]
    public async Task TestMessagesAreDispatched()
    {
        var mockHandler = new MockHandler();

        var listener = new MonoLambdaEventListener(Sqs, _eventHistoryFactory, config, [mockHandler],
            new NullLogger<MonoLambdaEventListener>());


        var messageBody = """
                          { "event_type": "mock"}
                          """;
        await listener.Handle(new Message { MessageId = "1234", Body = messageBody }, CancellationToken.None);
        Assert.Equal(1, mockHandler.CallCount);
    }
    
    [Fact]
    public async Task TestUnknownMessagesAreNotHandled()
    {
        var mockHandler = new MockHandler();

        var listener = new MonoLambdaEventListener(Sqs, _eventHistoryFactory, config, [mockHandler],
            new NullLogger<MonoLambdaEventListener>());


        var messageBody = """
                          { "event_type": "unknown"}
                          """;
        await listener.Handle(new Message { MessageId = "1234", Body = messageBody }, CancellationToken.None);
        Assert.Equal(0, mockHandler.CallCount);
    }

    [Fact]
    public void TestFailsIfDuplicateHandlersAreRegistered()
    {
        var mockHandler = new MockHandler();

        Assert.ThrowsAny<Exception>(() => new MonoLambdaEventListener(Sqs, _eventHistoryFactory, config,
            [mockHandler, mockHandler],
            new NullLogger<MonoLambdaEventListener>()));
    }
}