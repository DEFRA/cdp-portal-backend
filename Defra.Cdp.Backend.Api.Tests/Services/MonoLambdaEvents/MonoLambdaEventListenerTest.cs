using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.EventHistory;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambdaEvents;

internal class MockHandler : IMonoLambdaEventHandler
{
    public int callCount { get; set; }
    public string EventType => "mock";
    public bool PersistEvents => false;
    public Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        callCount += 1;
        return Task.CompletedTask;
    }
}


public class MonoLambdaEventListenerTest
{
    private readonly IEventHistoryFactory _eventHistoryFactory = Substitute.For<IEventHistoryFactory>();
    private readonly IAmazonSQS Sqs = Substitute.For<IAmazonSQS>();

    [Fact]
    public async Task TestMessagesAreDispatched()
    {
        var mockHandler = new MockHandler();
        IOptions<LambdaEventListenerOptions> config =
            new OptionsWrapper<LambdaEventListenerOptions>(
                new LambdaEventListenerOptions { QueueUrl = "http://queue.url", Enabled = true });

        var listener = new MonoLambdaEventListener(Sqs, _eventHistoryFactory, config, [mockHandler],
            new NullLogger<MonoLambdaEventListener>());


        var messageBody = """
                          { "event_type": "mock"}
                          """;
        await listener.Handle(new Message { MessageId = "1234", Body = messageBody }, CancellationToken.None);
        Assert.Equal(1, mockHandler.callCount);
    }

    [Fact]
    public void TestFailsIfDuplicateHandlersAreRegistered()
    {
        var mockHandler = new MockHandler();
        IOptions<LambdaEventListenerOptions> config =
            new OptionsWrapper<LambdaEventListenerOptions>(
                new LambdaEventListenerOptions { QueueUrl = "http://queue.url", Enabled = true });

        Assert.ThrowsAny<Exception>(() => new MonoLambdaEventListener(Sqs, _eventHistoryFactory, config,
            [mockHandler, mockHandler],
            new NullLogger<MonoLambdaEventListener>()));
    }
}