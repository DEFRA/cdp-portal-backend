using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public static class NotificationTypes
{
    public const string TestFailed = "testfailed";
    public const string TestPassed = "testpassed";
    public const string DeploymentFailed = "deploymentfailed";
    public const string DeploymentSuccess = "deploymentsuccess";
    public const string Shuttered = "shuttered";
    public const string Unshuttered = "unshuttered";
    public const string TenantResourceRequested = "tenantresourcerequested";

    public static readonly string[] All =
    [
        TestPassed,
        TestFailed,
        DeploymentFailed,
        DeploymentSuccess,
        Shuttered,
        Unshuttered,
        TenantResourceRequested
    ];
}

public interface INotificationEvent 
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    string EventType { get; }
    string? Entity { get; }
    string? Environment { get; } 
    public SlackMessageBody SlackMessage();
}

public class TestRunFailedEvent : INotificationEvent
{
    public string EventType => NotificationTypes.TestFailed;
    public required string Entity { get; init; }
    public string? Environment { get; init; }
    
    public required string RunId { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.TestFailedTemplate(this);
    }
}

public class TestRunPassedEvent : INotificationEvent
{
    public string EventType => NotificationTypes.TestPassed;
    public required string Entity { get; init; }
    public string? Environment { get; init; }
    
    public required string RunId { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.TestPassedTemplate(this);
    }
}

public class DeploymentFailedEvent : INotificationEvent
{
    public string EventType => NotificationTypes.DeploymentFailed;
    public required string Entity { get; init; }
    public required string? Environment { get; init; }
    public required string Version { get; init; }
    public required string DeploymentId { get; init; }
    public string? UserDisplayName { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.DeploymentFailedTemplate(this);
    }
}

public class DeploymentSuccessEvent : INotificationEvent
{
    public string EventType => NotificationTypes.DeploymentSuccess;
    public required string Entity { get; init; }
    public required string? Environment { get; init; }
    public required string Version { get; init; }
    public required string DeploymentId { get; init; }
    public string? UserDisplayName { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.DeploymentSuccessTemplate(this);
    }
}

public class ShutteredEvent : INotificationEvent
{
    public string EventType => NotificationTypes.Shuttered;
    public required string Entity { get; init; }
    public required string? Environment { get; init; }
    public required string Url { get; init; }
    public string? ActionedByDisplayName { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.ShutteredTemplate(this);
    }
}

public class UnshutteredEvent : INotificationEvent
{
    public string EventType => NotificationTypes.Unshuttered;
    public required string Entity { get; init; }
    public required string? Environment { get; init; }
    public required string Url { get; init; }
    public string? ActionedByDisplayName { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.UnshutteredTemplate(this);
    }
}

public class TenantResourceRequestedEvent : INotificationEvent
{
    public const string NotificationEntity = "tenant-resource-request";

    public string EventType => NotificationTypes.TenantResourceRequested;
    public string Entity => NotificationEntity;
    public string? Environment => null;

    public required string ServiceName { get; init; }
    public string? RequestedByDisplayName { get; init; }
    public string? RequestedByUserId { get; init; }
    public required string PullRequestUrl { get; init; }
    public required int PullRequestNumber { get; init; }
    public string? WorkflowRunUrl { get; init; }

    public SlackMessageBody SlackMessage()
    {
        return SlackMessageTemplates.TenantResourceRequestedTemplate(this);
    }
}
