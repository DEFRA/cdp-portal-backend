using System.Reflection;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;

public static class StatusHelper
{
    private static readonly List<List<string>> s_statusPrecedence =
    [
        new() { Status.NotRequested.ToStringValue() },
        new() { Status.Raised.ToStringValue(), Status.Requested.ToStringValue() },
        new() { Status.Queued.ToStringValue() },
        new() { Status.Merged.ToStringValue() },
        new() { Status.InProgress.ToStringValue() },
        new() { Status.Success.ToStringValue(), Status.Failure.ToStringValue() }
    ];

    public static List<string> DontOverwriteStatus(Status status)
    {
        var idx = s_statusPrecedence.FindIndex(t => t.Contains(status.ToStringValue()));
        return idx == -1 ? [] :
            // Get the statuses that come after the matched index and return them as a flat list.
            s_statusPrecedence.Skip(idx + 1).SelectMany(x => x).ToList();
    }

    public static List<string> GetStatusKeys(GithubReposOptions githubReposOptions, CreationType creationType)
    {
        var cdpTfSvcInfra = githubReposOptions.CdpTfSvcInfra;
        var cdpAppConfig = githubReposOptions.CdpAppConfig;
        var cdpNginxUpstreams = githubReposOptions.CdpNginxUpstreams;
        var cdpSquidProxy = githubReposOptions.CdpSquidProxy;
        var cdpGrafanaSvc = githubReposOptions.CdpGrafanaSvc;
        var cdpCreateWorkflows = githubReposOptions.CdpCreateWorkflows;

        return creationType switch
        {
            CreationType.Repository => [cdpCreateWorkflows],
            CreationType.JourneyTestsuite or CreationType.PerfTestsuite =>
            [
                cdpCreateWorkflows,
                cdpTfSvcInfra,
                cdpSquidProxy,
                cdpAppConfig
            ],
            CreationType.Microservice =>
            [
                cdpCreateWorkflows,
                cdpNginxUpstreams,
                cdpAppConfig,
                cdpTfSvcInfra,
                cdpSquidProxy,
                cdpGrafanaSvc
            ],
            _ => []
        };
    }


    public static Status NormaliseStatus(Status action, Status? conclusion)
    {
        switch (action)
        {
            case Status.Completed:
                switch (conclusion)
                {
                    case Status.Success:
                    case Status.Skipped:
                        return Status.Success;
                    case Status.Cancelled:
                        return Status.InProgress;
                    default:
                        return Status.Failure;
                }
            case Status.InProgress:
                return Status.InProgress;
            case Status.Queued:
                return Status.Queued;
            case Status.Requested:
                return Status.Requested;
            default:
                return Status.Requested;
        }
    }

    public static Status CalculateOverallStatus(GithubReposOptions reposOptions, LegacyStatus statusRecord)
    {
        var statusKeys = GetStatusKeys(reposOptions, statusRecord.Kind.ToType());

        var allSuccess = CheckAllKeysWithGivenStatus(statusRecord, statusKeys, Status.Success);

        var anyFailed = CheckAnyKeysWithGivenStatus(statusRecord, statusKeys, Status.Failure);

        if (allSuccess)
        {
            return Status.Success;
        }

        return anyFailed ? Status.Failure : Status.InProgress;
    }

    private static bool CheckAllKeysWithGivenStatus(LegacyStatus statusRecord, List<string> statusKeys, Status status)
    {
        var properties = statusRecord.GetType().GetProperties();
        return statusKeys.All(key =>
        {
            var keyProperty =
                properties.FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == key);

            return ((WorkflowDetails)statusRecord.GetType().GetProperty(keyProperty.Name)?.GetValue(statusRecord))
                ?.Status.ToStatus() == status;
        });
    }

    private static bool CheckAnyKeysWithGivenStatus(LegacyStatus statusRecord, List<string> statusKeys, Status status)
    {
        var properties = statusRecord.GetType().GetProperties();
        return statusKeys.Any(key =>
        {
            var keyProperty =
                properties.FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == key);

            return ((WorkflowDetails)statusRecord.GetType().GetProperty(keyProperty.Name)?.GetValue(statusRecord))
                ?.Status.ToStatus() == status;
        });
    }
}