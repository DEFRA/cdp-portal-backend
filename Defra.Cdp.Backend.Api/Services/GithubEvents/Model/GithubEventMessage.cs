using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;

namespace Defra.Cdp.Backend.Api.Services.GithubEvents.Model;

    public record GithubEventMessage
    {
        [JsonPropertyName("github_event")] public required string GithubEvent { get; init; }
        [JsonPropertyName("action")] public required string Action { get; init; }
        [JsonPropertyName("repository")] public Repository? Repository { get; init; }
        [JsonPropertyName("workflow_run")] public required WorkflowRun WorkflowRun { get; init; }
    }

    public class WorkflowRun
    {
        [JsonPropertyName("id")] public long? Id { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("head_branch")] public string? HeadBranch { get; init; }
        [JsonPropertyName("head_sha")] public string? HeadSha { get; init; }
        [JsonPropertyName("path")] public string? Path { get; init; }
        [JsonPropertyName("conclusion")] public string? Conclusion { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; init; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; init; }
    }

    public class Repository
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
    }
