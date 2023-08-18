using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IArtifactScanner
{
    Task<DeployableArtifact> ScanImage(string repo, string tag);
    Task<List<RescanRequest>> Backfill();
}

public class ArtifactScanner : IArtifactScanner
{
    // Used to determine what would have been extracted. we might want to use this for rescans etc.
    private const int DockerScannerVersion = 1;

    private readonly IDeployablesService _deployablesService;
    private readonly IDockerClient _dockerClient;


    // TOOD: refine the list of files we're interested in keeping.
    // I think the path may be important so we dont capture node modules etc
    private readonly List<Regex> _filesToExtract = new()
    {
        new Regex(".+/.+\\.deps\\.json$"), // TODO: find out what the WORKSPACE is in the c# base image
        new Regex("home/node.*/package-lock\\.json$"), // TODO: exclude anything in node_modules etc
        new Regex(".*/pom\\.xml$") // TODO: find out what our jvm image is going to look like and what the build system of choice is
    };

    private readonly ILayerService _layerService;

    private readonly ILogger _logger;

    // A list of paths we dont want to scan (stuff in the base image basically, avoids false positives
    private readonly List<Regex> _pathsToIgnore = new() { new Regex("^/?usr/.*") };

    public ArtifactScanner(
        IDeployablesService deployablesService,
        ILayerService layerService,
        IDockerClient dockerClient,
        ILogger<ArtifactScanner> logger)
    {
        _deployablesService = deployablesService;
        _layerService = layerService;
        _dockerClient = dockerClient;
        _logger = logger;
    }

    public async Task<DeployableArtifact> ScanImage(string repo, string tag)
    {
        var manifest = await _dockerClient.LoadManifest(repo, tag);
        if (manifest == null) throw new Exception($"Failed to load manifest for {repo}:{tag}");

        _logger.LogInformation("Downloading manifest for {Repo}:{Tag}...", repo, tag);

        // Extract the labels.
        var image = await _dockerClient.LoadManifestImage(repo, manifest.config);
        var labels = new Dictionary<string, string>();
        if (image != null) labels = image.config.Labels;

        _logger.LogInformation("Scanning layers in {Repo}:{Tag} for package.json...", repo, tag);

        // Search all the layers for files of interest.
        var searchLayerTasks = manifest.layers.Select(blob => SearchLayerUsingCache(repo, blob));
        var searchLayerResults = await Task.WhenAll(searchLayerTasks);


        // Flatten file list. Files in higher layers should overwrite ones in lower layers.
        // TODO: handle docker whiteout files.
        var mergedFiles = FlattenFiles(searchLayerResults);

        labels.TryGetValue("defra.cdp.git.repo.url", out var githubUrl);

        labels.TryGetValue("defra.cdp.service.name", out var serviceName);

        long semver = 0;

        try
        {
            semver = SemVer.SemVerAsLong(tag);
        }
        catch (Exception)
        {
            _logger.LogInformation("Invalid semver tag {Repo}:{Tag}", repo, tag);
        }

        // Persist the results.
        var artifact = new DeployableArtifact
        {
            ScannerVersion = DockerScannerVersion,
            Repo = repo,
            Tag = tag,
            Sha256 = manifest.config.digest,
            GithubUrl = githubUrl,
            ServiceName = serviceName,
            Files = mergedFiles.Values.ToList(),
            SemVer = semver
        };

        _logger.LogInformation("Saving artifact {Repo}:{Tag}...", repo, tag);
        await _deployablesService.CreateAsync(artifact);

        _logger.LogInformation("Artifact {Repo}:{Tag} completed", repo, tag);
        return artifact;
    }

    // The new backfill just generates a list of curl commands we can run from the terminal to trigger the backfill
    public async Task<List<RescanRequest>> Backfill()
    {
        _logger.LogInformation("Generating backfill script");

        var catalog = await _dockerClient.LoadCatalog();

        _logger.LogInformation("Backfill found {RepositoriesCount} repos to backfill", catalog.repositories.Count);

        // get ALL the tags for all the repos
        var rescanRequests = new List<RescanRequest>();
        foreach (var catalogRepository in catalog.repositories)
        {
            var tags = await _dockerClient.FindTags(catalogRepository);
            foreach (var tag in tags.tags)
            {
                _logger.LogInformation("Backfilling {CatalogRepository}:{Tag}", catalogRepository, tag);
                rescanRequests.Add(new RescanRequest(catalogRepository, tag));
            }
        }

        _logger.LogInformation("Backfill complete!");
        return rescanRequests;
    }


    private async Task<Layer> SearchLayerUsingCache(string repo, Blob blob)
    {
        var layer = await _layerService.Find(blob.digest);
        if (layer == null)
        {
            _logger.LogDebug("Layer {BlobDigest} not in cache, scanning...", blob.digest);
            layer = await _dockerClient.SearchLayer(repo, blob, _filesToExtract, _pathsToIgnore);
            await _layerService.CreateAsync(layer);
        }

        _logger.LogDebug("Layer {BlobDigest} loaded from cache", blob.digest);
        return layer;
    }

    public static Dictionary<string, DeployableArtifactFile> FlattenFiles(Layer[] searchLayerResults)
    {
        var mergedFiles = new Dictionary<string, DeployableArtifactFile>();
        searchLayerResults.ToList().ForEach(layer =>
        {
            foreach (var file in layer.Files)
                mergedFiles[file.FileName] =
                    new DeployableArtifactFile(Path.GetFileName(file.FileName), file.FileName, layer.Digest);
        });

        return mergedFiles;
    }
}