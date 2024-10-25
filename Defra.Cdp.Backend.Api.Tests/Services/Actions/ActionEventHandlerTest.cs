using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Actions;
using Defra.Cdp.Backend.Api.Services.Actions.events;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.Actions;

public class ActionEventHandlerTest
{
    [Fact]
    public async Task WillProcessSaveMessage()
    {
        var service = Substitute.For<IAppConfigEventService>();
        var eventHandler = new ActionEventHandler(service,
            ConsoleLogger.CreateLogger<ActionEventHandler>());

        var mockPayload = ActionEventHandler.TryParseMessageBody(
            @"{""action"": ""app-config"", ""content"" : {""commitSha"":""abc123"", ""commitTimestamp"":""2024-10-23T15:10:10.123"", ""environment"":""infra-dev""}}");

        Assert.NotNull(mockPayload);
        service
            .SaveMessage("abc123", DateTime.Now, "infra-dev", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await eventHandler.Handle(mockPayload, new CancellationToken());

        await service.ReceivedWithAnyArgs()
            .SaveMessage("abc123", Arg.Any<DateTime>(), "infra-dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TryParseMessageHeaderWithValidPayload()
    {
        var body =
            @"{""action"": ""app-config"", ""content"" : {""json"": ""anything""}}";
        var res = ActionEventHandler.TryParseMessageBody(body);
        Assert.NotNull(res);
    }

    [Fact]
    public void TryParseMessageHeaderInvalid()
    {
        var otherLambda =
            @"{""not-action"": ""something-else"", ""content"" : {""timestamp"":""2024-10-23T15:10:10.123"", ""environment"":""infra-dev""}}";
        var messageHeader = ActionEventHandler.TryParseMessageBody(otherLambda);
        Assert.Null(messageHeader);

        var otherMessage = "{\"foo\": \"bar\"}";
        messageHeader = ActionEventHandler.TryParseMessageBody(otherMessage);
        Assert.Null(messageHeader);

        var invalidJson = "<tag>foo</tag>";
        messageHeader = ActionEventHandler.TryParseMessageBody(invalidJson);
        Assert.Null(messageHeader);
    }

    [Fact]
    public void CanDeserialiseBody()
    {
        var message =
            @"{""action"": ""app-config"", 
                ""content"" : {
                    ""commitSha"":""abc123"", 
                    ""commitTimestamp"":""2024-10-23T15:10:10.123"", 
                    ""environment"":""infra-dev""}}";
        
        var messageHeader = ActionEventHandler.TryParseMessageBody(message);
        Assert.NotNull(messageHeader);

        var parsedBody = messageHeader.Content.Deserialize<AppConfigMessageContent>();
        Assert.Equal("infra-dev", parsedBody?.Environment);
        Assert.Equal("abc123", parsedBody?.CommitSha);
        Assert.Equal(new DateTime(2024, 10, 23, 15, 10, 10, 123), parsedBody?.CommitTimestamp);
    }
}