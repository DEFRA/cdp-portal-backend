using System.IO.Compression;
using System.Net.Http.Headers;
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
    Task<Layer> SearchLayer(string repo, Blob blob, List<Regex> filesToExtract, List<Regex> pathsToIgnore);
    Task<Catalog> LoadCatalog();
}

public class DockerClient : IDockerClient
{
    private readonly string baseUrl;

    private readonly HttpClient client;
    private readonly IDockerCredentialProvider credentialProvider;
    private readonly ILogger logger;

    public DockerClient(HttpClient client, IOptions<DockerServiceOptions> options,
        IDockerCredentialProvider credentialProvider, ILogger<DockerClient> logger)
    {
        this.client = client;
        this.logger = logger;
        baseUrl = options.Value.RegistryUrl;
        this.credentialProvider = credentialProvider;
        client.DefaultRequestHeaders.Accept.Clear();
    }

    public async Task<ImageTagList> FindTags(string repo)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get, RequestUri = new Uri($"{baseUrl}/v2/{repo}/tags/list")
        };
        req = await AddEcrAuthHeader(req);

        var response = await client.SendAsync(req);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var tagList = await JsonSerializer.DeserializeAsync<ImageTagList>(stream);
        if (tagList == null) throw new Exception($"Failed to get tag-list for ${repo}.");

        return tagList;
    }

    public async Task<Manifest?> LoadManifest(string repo, string tag)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{baseUrl}/v2/{repo}/manifests/{tag}"),
            Headers = { { "Accept", "application/vnd.docker.distribution.manifest.v2+json" } }
        };
        req = await AddEcrAuthHeader(req);

        var response = await client.SendAsync(req);

        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            logger.LogInformation(
                $"Failed to get manifest for {repo}:{tag}, response {response.StatusCode}: {msg}");
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<Manifest>(stream);
    }

    public async Task<ManifestImage?> LoadManifestImage(string repo, Blob blob)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{baseUrl}/v2/{repo}/blobs/{blob.digest}"),
            Headers = { { "Accept", blob.mediaType } }
        };
        req = await AddEcrAuthHeader(req); // ECR requires an API call to get the creds, hence the await

        var response = await client.SendAsync(req);
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<ManifestImage>(stream);
    }

    public async Task<Layer> SearchLayer(string repo, Blob blob, List<Regex> filesToExtract, List<Regex> pathsToIgnore)
    {
        var req = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{baseUrl}/v2/{repo}/blobs/{blob.digest}"),
            Headers = { { "Accept", blob.mediaType } }
        };
        req = await AddEcrAuthHeader(req);

        var response = await client.SendAsync(req);
        if (!response.IsSuccessStatusCode) throw new Exception($"Failed to get layer: {response.StatusCode}");

        var layerFiles = await Task.Run(() =>
            {
                using var stream = response.Content.ReadAsStream();
                return ExtractFilesFromStream(stream, $"{repo}:{blob.digest}", filesToExtract, pathsToIgnore);
            }
        );

        return new Layer(blob.digest, layerFiles);
    }

    public async Task<Catalog> LoadCatalog()
    {
        var req = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = new Uri($"{baseUrl}/v2/_catalog") };
        req = await AddEcrAuthHeader(req);

        var response = await client.SendAsync(req);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(stream);

        if (catalog == null) throw new Exception($"Failed to deserialize {req.RequestUri}");

        return catalog;
    }

    private async Task<HttpRequestMessage> AddEcrAuthHeader(HttpRequestMessage req)
    {
        var token = await credentialProvider.GetCredentials();
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return req;
    }

    public List<LayerFile> ExtractFilesFromStream(Stream stream, string sourceName, List<Regex> filesToExtract,
        List<Regex> pathsToIgnore)
    {
        var files = new List<LayerFile>();
        using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
        using (var tar = TarReader.Open(gzip))
        {
            while (tar.MoveToNextEntry())
            {
                if (tar.Entry.IsDirectory || pathsToIgnore.Any(prx => prx.IsMatch(tar.Entry.Key))) continue;

                // Right now we're just saving the text of the files we're interested in.
                // It may be an idea to extract the actual data (dropping bits we dont care about!)
                // or perhaps that should be handled off to another service and we keep the original text
                // so it can be reprocessed in the future (in which case may as well store it compressed)
                if (filesToExtract.Any(frx => frx.IsMatch(tar.Entry.Key)))
                {
                    logger.LogInformation($"Extracted {tar.Entry.Key} from ${sourceName} size: {tar.Entry.Size}");
                    var sr = new StreamReader(tar.OpenEntryStream());
                    var data = sr.ReadToEnd();
                    files.Add(new LayerFile(tar.Entry.Key, data));
                }
            }
        }

        return files;
    }
}