namespace Defra.Cdp.Backend.Api.Services.Notifications;

public record NotificationType(string Type, string[] AllowedParams, string Description);

public static class NotificationEventTypes
{
    public static readonly NotificationType TestRunPassed = new(
        Type: "TestRunPassed",
        AllowedParams: ["Environment", "Profile"],
        Description: "Fires when a test run passes."
    );


    public static readonly NotificationType TestRunFailed = new(
        Type: "TestRunFailed",
        AllowedParams: ["Environment", "Profile"],
        Description: "Fires when a test run fails."
    );
    
    
    public static readonly NotificationType DeploymentFailed = new(
        Type: "DeploymentFailed",
        AllowedParams: ["Environment"],
        Description: "Fires when a deployment fails."
    );

    public static readonly IReadOnlyList<NotificationType> All =
    [
        TestRunPassed, 
        TestRunFailed,
        DeploymentFailed
    ];

    public static readonly IReadOnlyDictionary<string, NotificationType> Map = 
        All.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
}