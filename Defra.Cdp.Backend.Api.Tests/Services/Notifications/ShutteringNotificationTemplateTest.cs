using Defra.Cdp.Backend.Api.Services.Notifications;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class ShutteringNotificationTemplateTest
{
    [Fact]
    public void Shuttered_template_contains_expected_values()
    {
        var message = new ShutteredEvent
        {
            Entity = "svc-a",
            Environment = "dev",
            Url = "foo.example.com",
            ActionedByDisplayName = "Jane Doe"
        }.SlackMessage();

        var header = message.Blocks?.FirstOrDefault()?.Text?.Text;
        var fields = message.Blocks?.Skip(1).FirstOrDefault()?.Fields?.Select(f => f.Text ?? string.Empty).ToList() ?? [];

        Assert.Equal("🟧 svc-a has been shuttered", header);
        Assert.Contains("*Environment:*\ndev", fields);
        Assert.Contains("*URL:*\nfoo.example.com", fields);
        Assert.Contains("*Actioned by:*\nJane Doe", fields);
    }

    [Fact]
    public void Unshuttered_template_contains_expected_values()
    {
        var message = new UnshutteredEvent
        {
            Entity = "svc-a",
            Environment = "dev",
            Url = "foo.example.com",
            ActionedByDisplayName = "Jane Doe"
        }.SlackMessage();

        var header = message.Blocks?.FirstOrDefault()?.Text?.Text;
        var fields = message.Blocks?.Skip(1).FirstOrDefault()?.Fields?.Select(f => f.Text ?? string.Empty).ToList() ?? [];

        Assert.Equal("✅ svc-a has been unshuttered", header);
        Assert.Contains("*Environment:*\ndev", fields);
        Assert.Contains("*URL:*\nfoo.example.com", fields);
        Assert.Contains("*Actioned by:*\nJane Doe", fields);
    }
}
