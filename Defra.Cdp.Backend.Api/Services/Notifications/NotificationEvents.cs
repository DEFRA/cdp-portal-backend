using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public static class NotificationTypes
{
    public const string TestFailed = "testfailed";
    public const string TestPassed = "testpassed";
    public const string DeploymentFailed = "deploymentfailed";

    public static readonly string[] All = [TestPassed, TestFailed, DeploymentFailed];
}

public interface INotificationEvent 
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    string EventType { get; }
    string? Entity { get; }
    string? Environment { get; } 
    public string SlackMessage();
}

public class TestRunFailedEvent : INotificationEvent
{
    public string EventType => NotificationTypes.TestFailed;
    public required string Entity { get; init; }
    public string? Environment { get; init; }
    
    public required string RunId { get; init; }

    public string SlackMessage()
    {
        // TODO: generate some nicer slack block messages
        return $"Test {Entity} failed in {Environment}";
    }
}

public class TestRunPassedEvent : INotificationEvent
{
    public string EventType => NotificationTypes.TestPassed;
    public required string Entity { get; init; }
    public string? Environment { get; init; }
    
    public required string RunId { get; init; }

    public string SlackMessage()
    {
        return $"Test: {Entity} passed in {Environment}";
    }
}

public class DeploymentFailed : INotificationEvent
{
    public string EventType => NotificationTypes.DeploymentFailed;
    public required string Entity { get; init; }
    public required string? Environment { get; init; }
    public required string Version { get; init; }
    public required string DeploymentId { get; init; }

    public string SlackMessage()
    {
        return $"{Entity} failed to deploy in {Environment}";
    }
}
