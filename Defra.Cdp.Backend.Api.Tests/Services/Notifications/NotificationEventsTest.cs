using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class NotificationEventsTest
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    [Fact]
    public void TestFailureMessageTemplate()
    {
        var e = new TestRunFailedEvent { Entity = "foo-backend", Environment = "prod", RunId = "1234" };
        var message = e.SlackMessage();
        var parsedMessage = JsonSerializer.Deserialize<SlackMessageTemplate>(message, _jsonOptions);
        Assert.Equal("header",  parsedMessage?.Blocks?[0].Type);
        Assert.Contains("foo-backend: Test run failed",  parsedMessage?.Blocks?[0].Text?.Text);
        Assert.Contains("prod",  parsedMessage?.Blocks?[1].Fields?[0].Text);
        Assert.Contains("/test-suites/test-results/prod/failed/foo-backend/1234/index.html",  parsedMessage?.Blocks?[1].Fields?[1].Text);
    }
    
    [Fact]
    public void TestPassedMessageTemplate()
    {
        var e = new TestRunPassedEvent() { Entity = "foo-backend", Environment = "prod", RunId = "1234" };
        var message = e.SlackMessage();
        var parsedMessage = JsonSerializer.Deserialize<SlackMessageTemplate>(message, _jsonOptions);
        Assert.Equal("header",  parsedMessage?.Blocks?[0].Type);
        Assert.Contains("foo-backend: Test run passed",  parsedMessage?.Blocks?[0].Text?.Text);
        Assert.Contains("prod",  parsedMessage?.Blocks?[1].Fields?[0].Text);
        Assert.Contains("/test-suites/test-results/prod/failed/foo-backend/1234/index.html",  parsedMessage?.Blocks?[1].Fields?[1].Text);
    }
}