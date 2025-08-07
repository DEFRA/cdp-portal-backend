using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Microsoft.Extensions.Options;
using SharpCompress.Readers.Tar;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDockerClient
{
    Task<ImageTagList> FindTags(string repo);
    Task<Manifest?> LoadManifest(string repo, string tag);
    Task<ManifestImage?> LoadManifestImage(string repo, Blob blob);
    Task<Catalog> LoadCatalog(CancellationToken cancellationToken);
}

public class DockerClient : IDockerClient
{
    private readonly string _baseUrl;

    private readonly HttpClient _client;
    private readonly IDockerCredentialProvider _credentialProvider;
    private readonly ILogger _logger;

    public DockerClient(
        HttpClient client,
        IOptions<DockerServiceOptions> options,
        IDockerCredentialProvider credentialProvider,
        ILogger<DockerClient> logger)
    {
        _client = client;
        _logger = logger;
        _baseUrl = options.Value.RegistryUrl;
        _credentialProvider = credentialProvider;
        _client.DefaultRequestHeaders.Accept.Clear();
    }

    public async Task<ImageTagList> FindTags(string repo)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_baseUrl}/v2/{repo}/tags/list")
        };
        req = await AddEcrAuthHeader(req);

        var response = await _client.SendAsync(req);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var tagList = await JsonSerializer.DeserializeAsync<ImageTagList>(stream);
        if (tagList == null) throw new Exception($"Failed to get tag-list for {repo}.");

        return tagList;
    }

    public async Task<Manifest?> LoadManifest(string repo, string tag)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_baseUrl}/v2/{repo}/manifests/{tag}"),
            Headers = { { "Accept", "application/vnd.docker.distribution.manifest.v2+json" } }
        };
        req = await AddEcrAuthHeader(req);

        var response = await _client.SendAsync(req);

        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Failed to get manifest for {Repo}:{Tag}, response {ResponseStatusCode}: {Msg}",
                repo, tag, response.StatusCode, msg);
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync();
        var manifest = JsonSerializer.Deserialize<Manifest>(content);
        if (manifest != null)
            manifest.digest = "sha256:" + Convert.ToHexString(SHA256.Create().ComputeHash(content));
        return manifest;
    }

    public async Task<ManifestImage?> LoadManifestImage(string repo, Blob blob)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_baseUrl}/v2/{repo}/blobs/{blob.digest}"),
            Headers = { { "Accept", blob.mediaType } }
        };
        req = await AddEcrAuthHeader(req); // ECR requires an API call to get the creds, hence the await

        var response = await _client.SendAsync(req);
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<ManifestImage>(stream);
    }

    public async Task<Catalog> LoadCatalog(CancellationToken cancellationToken)
    {
        var req = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri($"{_baseUrl}/v2/_catalog") };
        req = await AddEcrAuthHeader(req);

        var response = await _client.SendAsync(req, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(stream, cancellationToken: cancellationToken);

        if (catalog == null) throw new Exception($"Failed to deserialize {req.RequestUri}");

        return catalog;
    }

    private async Task<HttpRequestMessage> AddEcrAuthHeader(HttpRequestMessage req)
    {
        var token = await _credentialProvider.GetCredentials();
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return req;
    }
}