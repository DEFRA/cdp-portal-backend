using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.GithubEvents.Model;

public record LegacyStatus
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }
    
    [property: JsonPropertyName("org")] public string Org { get; set; }

    [property: JsonPropertyName("repositoryName")]
    public string RepositoryName { get; set; }

    [property: JsonPropertyName("portalVersion")]
    public long PortalVersion { get; set; }

    [property: JsonPropertyName("kind")] public string Kind { get; set; }
    [property: JsonPropertyName("status")] public string Status { get; set; }

    [property: JsonPropertyName("started")]
    public DateTime Started { get; set; }

    [property: JsonPropertyName("serviceTypeTemplate")]
    public string ServiceTypeTemplate { get; set; }

    [property: JsonPropertyName("team")] public Team Team { get; set; }

    [property: JsonPropertyName("creator")]
    public Creator Creator { get; set; }

    [property: JsonPropertyName("zone")] public string Zone { get; set; }
    
    [property: JsonPropertyName("cdp-create-workflows")]
    [BsonElement("cdp-create-workflows")]
    [BsonIgnoreIfNull]
    public WorkflowDetails CdpCreateWorkflows { get; set; }

    [property: JsonPropertyName("cdp-tf-svc-infra")]
    [BsonElement("cdp-tf-svc-infra")]
    [BsonIgnoreIfNull]
    public WorkflowDetails? CdpTfSvcInfra { get; set; }

    [property: JsonPropertyName("cdp-squid-proxy")]
    [BsonElement("cdp-squid-proxy")]
    [BsonIgnoreIfNull]
    public WorkflowDetails? CdpSquidProxy { get; set; }

    [property: JsonPropertyName("cdp-app-config")]
    [BsonElement("cdp-app-config")]
    [BsonIgnoreIfNull]
    public WorkflowDetails? CdpAppConfig { get; set; }

    [property: JsonPropertyName("cdp-nginx-upstreams")]
    [BsonElement("cdp-nginx-upstreams")]
    [BsonIgnoreIfNull]
    public WorkflowDetails? CdpNginxUpstreams{ get; set; }

    [property: JsonPropertyName("cdp-grafana-svc")]
    [BsonElement("cdp-grafana-svc")]
    [BsonIgnoreIfNull]
    public WorkflowDetails? CdpGrafanaSvc{ get; set; }
}

public class Team
{
    [property: JsonPropertyName("teamId")] public string TeamId { get; set; }
    [property: JsonPropertyName("name")] public string Name { get; set; }
}

public class Creator
{
    [property: JsonPropertyName("id")] public string Id { get; set; }

    [property: JsonPropertyName("displayName")]
    public string DisplayName { get; set; }
}

public class WorkflowDetails
{
    [property: JsonPropertyName("status")]
    public string Status { get; set; }

    [property: JsonPropertyName("result")]
    public string Result { get; set; }

    [property: JsonPropertyName("trigger")]
    public Trigger Trigger { get; set; }

    [property: JsonPropertyName("main")]
    [BsonIgnoreIfNull]
    public Main? Main { get; set; }
}

public class Trigger
{
    [property: JsonPropertyName("org")]
    public string Org { get; set; }

    [property: JsonPropertyName("repo")]
    public string Repo { get; set; }

    [property: JsonPropertyName("workflow")]
    public string Workflow { get; set; }

    [property: JsonPropertyName("inputs")]
    public TriggerInputs Inputs { get; set; }
}

public class TriggerInputs
{
    [property: JsonPropertyName("serviceTypeTemplate")]
    public string ServiceTypeTemplate { get; set; }
    
    [property: JsonPropertyName("templateTag")]
    public string TemplateTag { get; set; }

    [property: JsonPropertyName("repositoryName")]
    public string RepositoryName { get; set; }

    [property: JsonPropertyName("team")]
    public string Team { get; set; }

    [property: JsonPropertyName("additionalGitHubTopics")]
    public string AdditionalGitHubTopics { get; set; }

    [property: JsonPropertyName("service")]
    public string Service { get; set; }

    [property: JsonPropertyName("zone")]
    public string Zone { get; set; }

    [property: JsonPropertyName("mongo_enabled")]
    [BsonElement("mongo_enabled")]
    public string MongoEnabled { get; set; }

    [property: JsonPropertyName("redis_enabled")]
    [BsonElement("redis_enabled")]
    public string RedisEnabled { get; set; }

    [property: JsonPropertyName("service_code")]
    [BsonElement("service_code")]
    public string ServiceCode { get; set; }

    [property: JsonPropertyName("test_suite")]
    [BsonElement("test_suite")]
    public string TestSuite { get; set; }

    [property: JsonPropertyName("service_zone")]
    [BsonElement("service_zone")]
    public string ServiceZone { get; set; }
}

public class Main
{
    [property: JsonPropertyName("workflow")]
    public Workflow? Workflow { get; set; }
}

public class Workflow
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("id")]
    public long Id { get; set; }

    [property: JsonPropertyName("html_url")]
    [BsonElement("html_url")]
    public string HtmlUrl { get; set; }

    [property: JsonPropertyName("created_at")]
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [property: JsonPropertyName("updated_at")]
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [property: JsonPropertyName("path")]
    public string Path { get; set; }
}

public enum CreationType
{
    Repository,
    JourneyTestsuite,
    PerfTestsuite,
    Microservice
}


public static class CreationTypeExtensions
{
    public static string ToStringValue(this CreationType kind)
    {
        switch (kind)
        {
            case CreationType.Repository: return "repository";
            case CreationType.JourneyTestsuite: return "journey-testsuite";
            case CreationType.PerfTestsuite: return "perf-testsuite";
            case CreationType.Microservice: return "microservice";
            default: throw new ArgumentOutOfRangeException();
        }
    }
    public static CreationType ToType(this string kind)
    {
        switch (kind)
        {
            case "repository": return CreationType.Repository;
            case "journey-testsuite": return CreationType.JourneyTestsuite;
            case "perf-testsuite": return CreationType.PerfTestsuite;
            case "microservice": return CreationType.Microservice;
            default: throw new ArgumentOutOfRangeException();
        }
    }
}

public enum Status
{
    Cancelled,
    NotRequested,
    Requested,
    Completed,
    Raised,
    Queued,
    Merged,
    InProgress,
    GithubInProgress,
    Skipped,
    Success,
    Failure
}

public static class StatusExtensions
{
    public static string ToStringValue(this Status status)
    {
        switch (status)
        {
            case Status.Cancelled: return "cancelled";
            case Status.NotRequested: return "not-requested";
            case Status.Requested: return "requested";
            case Status.Completed: return "completed";
            case Status.Raised: return "raised";
            case Status.Queued: return "queued";
            case Status.Merged: return "merged";
            case Status.InProgress: return "in-progress";
            case Status.GithubInProgress: return "in_progress";
            case Status.Skipped: return "skipped";
            case Status.Success: return "success";
            case Status.Failure: return "failure";
            default: throw new ArgumentOutOfRangeException();
        }
    }
    public static Status ToStatus(this string status)
    {
        switch (status)
        {
            case "cancelled": return Status.Cancelled;
            case "not-requested": return Status.NotRequested;
            case "requested": return Status.Requested;
            case "completed": return Status.Completed;
            case "raised": return Status.Raised;
            case "queued": return Status.Queued;
            case "merged": return Status.Merged;
            case "in-progress": return Status.InProgress;
            case "in_progress": return Status.GithubInProgress;
            case "skipped": return Status.Skipped;
            case "success": return Status.Success;
            case "failure": return Status.Failure;
            default: throw new ArgumentOutOfRangeException();
        }
    }
}