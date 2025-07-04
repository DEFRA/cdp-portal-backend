using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Utils.Clients;

public class SelfServiceOpsClient
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

    public async Task TriggerTestSuite(string imageName, UserDetails user, Deployment deployment,
        TestRunSettings? testRunSettings, CancellationToken cancellationToken)
    {
        const int defaultTestSuiteCpu = 4096; // 4 vCPU
        const int defaultTestSuiteMemory = 8192; // 8 GB

        var body = new
        {
            imageName,
            environment = deployment.Environment,
            cpu = testRunSettings?.Cpu ?? defaultTestSuiteCpu,
            memory = testRunSettings?.Memory ?? defaultTestSuiteMemory,
            user,
            deployment = new DeploymentDetails
            {
                DeploymentId = deployment.CdpDeploymentId,
                Service = deployment.Service,
                Version = deployment.Version
            }
        };
        await SendAsyncWithSignature("/trigger-test-suite", JsonSerializer.Serialize(body), HttpMethod.Post, cancellationToken);
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
        await SendAsyncWithSignature("/auto-deploy-service", JsonSerializer.Serialize(body), HttpMethod.Post, cancellationToken);
    }

    public async Task TriggerDecommissionWorkflows(string entityName, CancellationToken cancellationToken)
    {
        var httpMethod = HttpMethod.Delete;
        var path = $"/decommission/{entityName}";
        var body = "";

        await SendAsyncWithSignature(path, body, httpMethod, cancellationToken);
    }

    private async Task SendAsyncWithSignature(string path, string serializedBody, HttpMethod httpMethod, CancellationToken cancellationToken)
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
        if (serializedBody != "")
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

public class User
{
    [property: JsonPropertyName("id")] public string? Id { get; init; }

    [property: JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}