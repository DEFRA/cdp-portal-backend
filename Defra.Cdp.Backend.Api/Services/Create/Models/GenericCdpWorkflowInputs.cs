using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

/// <summary>
/// Represents the payload to be passed to the GitHub Generic CDP CLI workflow.
/// </summary>
/// <param name="commands">A stringified json array containing the commands to run</param>
/// <param name="runId">Unique ID to track the workflow</param>
/// <param name="useBranch">Name of the branch to raise the PR from</param>
/// <param name="prTitle">PR Title</param>
public class GenericCdpWorkflowInputs(List<string> commands, string? runId, string? useBranch, string? prTitle)
{
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; } = runId;

    [JsonPropertyName("commands")]
    public string Commands { get; init; } = JsonSerializer.Serialize(commands, s_serializerOptions);

    [JsonPropertyName("use_branch")]
    public string? UseBranch { get; init; } = useBranch;

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; init; } = prTitle;
}
