using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Notifications;

public record NotificationOptions
{
    public required string EventType { get; init; }
    public List<string>? Environments { get; init; }
}

/// <summary>
/// For a given entity work out what kinds of notifications are relevant to it
/// </summary>
public static class NotificationOptionLookup
{
    public static List<NotificationOptions> FindOptionsForEntity(Entity entity)
    {
        List<NotificationOptions> options = [];
        // Hard coded logic for now

        switch (entity.Type)
        {
            case Type.Microservice:
                // Deployment Failures
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.DeploymentFailed,
                    Environments = entity.Environments.Keys.ToList()
                });
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.DeploymentSuccess,
                    Environments = entity.Environments.Keys.ToList()
                });

                // Shuttering
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.Shuttered,
                    Environments = entity.Environments.Keys.ToList()
                });
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.Unshuttered,
                    Environments = entity.Environments.Keys.ToList()
                });
                break;
            case Type.TestSuite:
                var envs = entity.Environments.Keys.ToList();
                if (entity.SubType == SubType.Performance)
                {
                    envs = [CdpEnvironments.PerfTest];
                }
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.TestFailed,
                    Environments = envs
                });
                options.Add(new NotificationOptions
                {
                    EventType = NotificationTypes.TestPassed,
                    Environments = envs
                });
                break;
            case Type.Repository:
            default:
                break;
        }

        return options;
    }
}