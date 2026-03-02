namespace Defra.Cdp.Backend.Api.Services.Notifications;

public interface INotificationEvent 
{
    string EventType { get; }
    string Entity { get; } 
    Dictionary<string, string> Context { get; }
    public string Message(); // TODO: This assumes we're generating a slack message. we can extend this for other kinds
}

public class TestRunFailedEvent : INotificationEvent
{
    public string EventType => NotificationEventTypes.TestRunFailed.Type;
    public required string Entity { get; set; }
    public required string Environment { get; set; }
    public required string RunId { get; set; }

    public Dictionary<string, string> Context => new()
    {
        { "environment", Environment },
        { "runid", RunId },
    };
    
    public string Message()
    {
        // TODO: generate some nicer slack block messages
        return $"Test {Entity} failed in {Environment}";
    }
}

public class TestRunPassedEvent : INotificationEvent
{
    public string EventType => NotificationEventTypes.TestRunPassed.Type;
    public required string Entity { get; set; }
    public required string Environment { get; set; }
    public required string RunId { get; set; }
    
    public Dictionary<string, string> Context => new()
    {
        { "environment", Environment },
        { "runid", RunId }
    };
    
    public string Message()
    {
        return $"Test: {Entity} passed in {Environment}";
    }
}

public class DeploymentFailed : INotificationEvent
{
    public string EventType => NotificationEventTypes.DeploymentFailed.Type;
    public required string Entity { get; set; }
    public required string Version { get; set; }
    public required string Environment { get; set; }
    public required string DeploymentId { get; set; }
    
    public Dictionary<string, string> Context => new()
    {
        { "environment", Environment },
        { "deploymentid", DeploymentId },
        { "version", Version }
    };
    
    public string Message()
    {
        return $"{Entity} failed to deploy in {Environment}";
    }
}
