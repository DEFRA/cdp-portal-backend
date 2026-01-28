using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Dependencies;


public interface ISbomExplorerClient
{
    public Task PushRunningServices(string env, List<Deployment> deployments, CancellationToken cancellationToken);
    public Task PushLatestVersions(List<ArtifactVersion> versions, CancellationToken cancellationToken);
}

/// <summary>
/// A stub version of the client, for use locally when we're not running sbom explorer
/// </summary>
public class NoOpSbomExplorerClient : ISbomExplorerClient
{
    public Task PushRunningServices(string env, List<Deployment> deployments, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PushLatestVersions(List<ArtifactVersion> versions, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


public class SbomExplorerClient : ISbomExplorerClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public SbomExplorerClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("SbomExplorerBackendUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new Exception("SbomExplorerBackendUrl cannot be null");
        _client = httpClientFactory.CreateClient("ServiceClient");
    }
    
    public async Task PushRunningServices(string env, List<Deployment> deployments, CancellationToken cancellationToken)
    {
        var payload = new SbomVersionPayload
        {
            Environment = env, 
            Versions = deployments.Select(d => new SbomVersion { Name = d.Service, Version = d.Version }).ToList()
        };

        var uri = new UriBuilder(_baseUrl) { Path = "/deployments/update" }.Uri;
        var result = await _client.PostAsJsonAsync(uri, payload, cancellationToken);
        result.EnsureSuccessStatusCode();
    }
    
    public async Task PushLatestVersions(List<ArtifactVersion> versions, CancellationToken cancellationToken)
    {
        var payload = new SbomVersionPayload
        {
            Environment = "latest",
            Versions = versions.Select(d => new SbomVersion { Name = d.Name, Version = d.Version }).ToList()
        };

        var uri = new UriBuilder(_baseUrl) { Path = "/deployments/update" }.Uri;
        var result = await _client.PostAsJsonAsync(uri, payload, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    record SbomVersion
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }
    }
    
    record SbomVersionPayload
    {
        [JsonPropertyName("environment")]
        public required string Environment { get; init; }

        [JsonPropertyName("versions")] 
        public List<SbomVersion> Versions { get; init; } = [];
    }
}