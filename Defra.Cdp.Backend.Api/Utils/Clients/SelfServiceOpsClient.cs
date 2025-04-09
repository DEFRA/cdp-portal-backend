using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Utils.Clients;

public class SelfServiceOpsClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public SelfServiceOpsClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("SelfServiceOpsUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new Exception("Self service ops backend url cannot be null");
        _client = httpClientFactory.CreateClient("ServiceClient");
    }

    public async Task TriggerTestSuite(string imageName, string environment, UserDetails user,
        TestRunSettings? testRunSettings, CancellationToken cancellationToken)
    {
        const int defaultTestSuiteCpu = 4096; // 4 vCPU
        const int defaultTestSuiteMemory = 8192; // 8 GB

        var body = new
        {
            imageName,
            environment,
            cpu = testRunSettings?.Cpu ?? defaultTestSuiteCpu,
            memory = testRunSettings?.Memory ?? defaultTestSuiteMemory,
            user
        };
        var payload = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var result = await _client.PostAsync(_baseUrl + "/trigger-test-suite", payload, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public async Task AutoDeployService(string imageName, string version, string environment,
        UserDetails user,
        DeploymentSettings deploymentSettings,
        string configVersion,
        CancellationToken cancellationToken)
    {
        const string defaultCpu = "1024"; // 1 vCPU
        const string defaultMemory = "2048"; // 2 GB
        const int defaultInstanceCount = 1;

        var body = new
        {
            imageName,
            version,
            environment,
            user,
            cpu = deploymentSettings.Cpu ?? defaultCpu,
            memory = deploymentSettings.Memory ?? defaultMemory,
            instanceCount = deploymentSettings.InstanceCount ?? defaultInstanceCount,
            configVersion
        };
        var payload = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var result = await _client.PostAsync(_baseUrl + "/auto-deploy-service", payload, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public async Task<CreationStatus?> FindStatus(string service)
    {
        var response = await _client.GetAsync($"{_baseUrl}/status/{service}");
        if (!response.IsSuccessStatusCode) return null;
        var stream = await response.Content.ReadAsStreamAsync();
        var json = await JsonSerializer.DeserializeAsync<JsonObject>(stream);
        var status = json.Deserialize<SelfServiceOpsStatus>();
        if (status?.RepositoryStatus == null)
        {
            return null;
        }

        return new CreationStatus
        {
            Status = status.RepositoryStatus.Status,
            Content = json,
            Creator = status.RepositoryStatus.Creator,
            Kind = status.RepositoryStatus.Kind,
            Started = status.RepositoryStatus.Started
        };
    }
}

public class CreationStatus
{
    public required string Status { get; init; }
    public string? Kind { get; init; }
    public DateTime? Started { get; init; }
    public User? Creator { get; init; }
    public JsonObject? Content { get; set; }
}

public class SelfServiceOpsStatus
{
    [property: JsonPropertyName("repositoryStatus")]
    public RepoStatus? RepositoryStatus { get; set; }

    public class RepoStatus
    {
        [property: JsonPropertyName("status")] public required string Status { get; init; }

        [property: JsonPropertyName("kind")] public required string Kind { get; init; }

        [property: JsonPropertyName("started")]
        public DateTime? Started { get; init; }

        [property: JsonPropertyName("creator")]
        public User? Creator { get; init; }
    }
}

public class User
{
    [property: JsonPropertyName("id")] string? Id { get; init; }

    [property: JsonPropertyName("displayName")]
    string? DisplayName { get; init; }
}