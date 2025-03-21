using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStatus
{
    public const string Requested = "requested";
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Stopped = "stopped";
    public const string Stopping = "stopping";
    public const string Failed = "failed";
    public const string Undeployed = "undeployed";

    public const string SERVICE_DEPLOYMENT_COMPLETED = "SERVICE_DEPLOYMENT_COMPLETED";
    public const string SERVICE_DEPLOYMENT_FAILED = "SERVICE_DEPLOYMENT_FAILED";
    public const string SERVICE_DEPLOYMENT_IN_PROGRESS = "SERVICE_DEPLOYMENT_IN_PROGRESS";

    // How many extra stopped deployments in how many minutes should be considered as a crash-loop
    private static readonly double s_UnstableWindowMins = 6;
    private static readonly int s_UnstableThreshold = 4;


    public static string? CalculateStatus(string desired, string last)
    {
        return desired switch
        {
            "RUNNING" => last switch
            {
                "PROVISIONING" => Pending,
                "PENDING" => Pending,
                "ACTIVATING" => Pending,
                "RUNNING" => Running,
                _ => null
            },
            "STOPPED" => last switch
            {
                "DEACTIVATING" => Stopping,
                "STOPPING" => Stopping,
                "DEPROVISIONING" => Stopping,
                "STOPPED" => Stopped,
                _ => null
            },
            _ => null
        };
    }

    public static bool IsUnstable(Deployment d)
    {
        // No extra instances means its fine
        if (d.Instances.Count <= d.InstanceCount) return false;

        int alive = 0;
        int dead = 0;

        var recently = DateTime.Now.Subtract(TimeSpan.FromMinutes(s_UnstableWindowMins));

        foreach (var i in d.Instances.Values.Where(i => i.Updated > recently))
        {
            switch (i.Status)
            {
                case Pending or Running:
                    alive++;
                    break;
                case Stopping or Stopped:
                    dead++;
                    break;
            }
        }

        // If we have some that are alive we know its not suppose to be stopped
        // And if the number of dead are above the threshold then we report the crash-loop
        return alive > 0 && dead >= s_UnstableThreshold;
    }

    public static string CalculateOverallStatus(Deployment d)
    {
        // Undeployments will never have any more events
        if (d.InstanceCount == 0)
        {
            return Undeployed;
        }

        var instances = d.Instances
            .Values
            .GroupBy(v => v.Status)
            .ToDictionary(g => g.Key, g => g.Count());


        // If we have all the running instances we desire
        if (instances.GetValueOrDefault(Running, 0) >= d.InstanceCount)
        {
            return d.LastDeploymentStatus == SERVICE_DEPLOYMENT_COMPLETED ? Running : Pending;
        }

        // If we have anything pending, it means we're not done yet...
        if (instances.GetValueOrDefault(Pending, 0) > 0)
        {
            return Pending;
        }

        // The service is shutting down
        if (instances.GetValueOrDefault(Stopping, 0) > 0)
        {
            return Stopping;
        }

        // One service has started but the other task has yet to begin
        if (instances.GetValueOrDefault(Running, 0) > 0)
        {
            return Pending;
        }

        if (d.LastDeploymentStatus == SERVICE_DEPLOYMENT_FAILED)
        {
            return Failed;
        }

        // To prevent `Stopped` being set straight after Requesting a deployment, but before we get instance information
        if (d.Status == Requested)
        {
            return Requested;
        }

        // Otherwise, with nothing running, pending, or stopping the deployment must have been stopped (right?)
        return Stopped;
    }
}