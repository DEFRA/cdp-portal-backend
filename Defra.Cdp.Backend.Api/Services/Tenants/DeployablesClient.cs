using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeployablesClient
{
    Task<DeployableArtifactId?> LookupImage(string image);
}

public class DeployablesClient : IDeployablesClient
{
    private readonly DeployablesClientOptions cfg;
    private readonly HttpClient http;

    public DeployablesClient(IHttpClientFactory httpClientFactory, IOptions<DeployablesClientOptions> cfg)
    {
        http = httpClientFactory.CreateClient("deployables");
        this.cfg = cfg.Value;
    }

    public async Task<DeployableArtifactId?> LookupImage(string image)
    {
        var (repo, tag) = SplitImage(image);
        if (repo == null || tag == null) return null;
        var path = $"{cfg.BaseUri}/artifacts/{repo}/{tag}";
        var result = await http.GetAsync(path);
        if (result.StatusCode == HttpStatusCode.NotFound) return null;

        await using var stream = await result.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<DeployableArtifactId>(stream);
    }

    public static (string?, string?) SplitImage(string image)
    {
        var rx = new Regex("^.+\\/(.+):(.+)$");
        var result = rx.Match(image);
        if (result.Groups.Count == 3) return (result.Groups[1].Value, result.Groups[2].Value);

        return (null, null);
    }
}