using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Utils.Clients;

public interface ISelfServiceOpsClient
{
    Task TriggerTestSuite(string testSuite, UserDetails user, string environment, TestRunSettings? testRunSettings,
        string? profile, Deployment? deployment, CancellationToken ct);

    Task AutoDeployService(string imageName, string version, string environment, UserDetails user,
        DeploymentSettings deploymentSettings, string configVersion, CancellationToken ct);

    Task TriggerDecommissionWorkflow(string entityName, CancellationToken ct);
    Task ScaleEcsToZero(string entityName, UserDetails user, CancellationToken ct);
}

public class SelfServiceOpsClient : ISelfServiceOpsClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;
    private readonly string? _selfServiceOpsSecret;

    public SelfServiceOpsClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("SelfServiceOpsUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new Exception("Self service ops backend url cannot be null");
        _client = httpClientFactory.CreateClient("ServiceClient");
        _selfServiceOpsSecret = configuration.GetValue<string>("SelfServiceOpsSecret");
    }

    public async Task TriggerTestSuite(string testSuite, UserDetails user, string environment,
        TestRunSettings? testRunSettings, string? profile, Deployment? deployment, CancellationToken ct)
    {
        const int defaultTestSuiteCpu = 4096; // 4 vCPU
        const int defaultTestSuiteMemory = 8192; // 8 GB

        var request = new TriggerTestSuiteRequest
        {
            TestSuite = testSuite,
            Environment = environment,
            Cpu = testRunSettings?.Cpu ?? defaultTestSuiteCpu,
            Memory = testRunSettings?.Memory ?? defaultTestSuiteMemory,
            User = user,
            Profile = profile,
        };

        if (deployment != null)
        {
            request.Deployment = new DeploymentDetails
            {
                DeploymentId = deployment.CdpDeploymentId,
                Service = deployment.Service,
                Version = deployment.Version
            };
        }

        await SendAsyncWithSignature("/trigger-test-suite", JsonSerializer.Serialize(request), HttpMethod.Post,
            ct);
    }

    public async Task AutoDeployService(string imageName, string version, string environment,
        UserDetails user,
        DeploymentSettings deploymentSettings,
        string configVersion,
        CancellationToken ct)
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
        await SendAsyncWithSignature("/auto-deploy-service", JsonSerializer.Serialize(body), HttpMethod.Post,
            ct);
    }

    public async Task TriggerDecommissionWorkflow(string entityName, CancellationToken ct)
    {
        var httpMethod = HttpMethod.Post;
        var path = $"/decommission/{entityName}/trigger-workflow";

        await SendAsyncWithSignature(path, null, httpMethod, ct);
    }

    public async Task ScaleEcsToZero(string entityName, UserDetails user, CancellationToken ct)
    {
        var httpMethod = HttpMethod.Post;
        var path = $"/decommission/{entityName}/scale-ecs-to-zero";
        await SendAsyncWithSignature(path, JsonSerializer.Serialize(user), httpMethod, ct);
    }

    private async Task SendAsyncWithSignature(string path, string? serializedBody, HttpMethod httpMethod,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var method = httpMethod.ToString().ToUpper();
        var message = $"{method}\n{path}\n{timestamp}\n{serializedBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_selfServiceOpsSecret ??
                                                               throw new InvalidOperationException(
                                                                   "SelfServiceOpsSecret is not configured.")));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToHexString(hash);


        var request = new HttpRequestMessage(httpMethod, _baseUrl + path);
        if (serializedBody != null)
        {
            request.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
        }

        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Signature-Version", "v1");

        var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public class TriggerTestSuiteRequest
{
    [JsonPropertyName("testSuite")] public string TestSuite { get; init; } = default!;

    [JsonPropertyName("environment")] public string Environment { get; init; } = default!;

    [JsonPropertyName("cpu")] public int Cpu { get; init; }

    [JsonPropertyName("memory")] public int Memory { get; init; }

    [JsonPropertyName("user")] public UserDetails User { get; init; } = default!;

    [JsonPropertyName("deployment")] public DeploymentDetails? Deployment { get; set; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}