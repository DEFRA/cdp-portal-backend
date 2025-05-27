using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IArtifactScanner
{
    Task<ArtifactScannerResult> ScanImage(string repo, string tag, CancellationToken cancellationToken);
    Task<List<RescanRequest>> Backfill(CancellationToken cancellationToken);
}

public class ArtifactScannerResult
{
    public readonly DeployableArtifact? Artifact;
    public readonly string Error;
    public readonly bool Success;

    public ArtifactScannerResult(DeployableArtifact? artifact)
    {
        Artifact = artifact;
        Success = true;
        Error = "";
    }

    private ArtifactScannerResult(DeployableArtifact? artifact, bool success, string error)
    {
        Artifact = artifact;
        Success = success;
        Error = error;
    }

    public static ArtifactScannerResult Failure(string reason)
    {
        return new ArtifactScannerResult(null, false, reason);
    }
}

public class ArtifactScanner : IArtifactScanner
{
    // Used to determine what would have been extracted. we might want to use this for rescans etc.
    private const int DockerScannerVersion = 1;

    private readonly IDeployableArtifactsService _deployableArtifactsService;
    private readonly IDockerClient _dockerClient;


    // TOOD: refine the list of files we're interested in keeping.
    // I think the path may be important so we dont capture node modules etc
    private readonly List<Regex> _filesToExtract = new()
    {
        // TODO: Uncomment this once we're ready to do something with this data!
        //new Regex(".+/.+\\.deps\\.json$"),
        //new Regex("home/node.*/package-lock\\.json$"),
        //new Regex(".*/pom\\.xml$")
    };

    private readonly ILayerService _layerService;

    private readonly ILogger _logger;

    // A list of paths we dont want to scan (stuff in the base image basically, avoids false positives
    private readonly List<Regex> _pathsToIgnore = new() { new Regex("^/?usr/.*") };

    private readonly IRepositoryService _repositoryService;

    public ArtifactScanner(
        IDeployableArtifactsService deployableArtifactsService,
        ILayerService layerService,
        IDockerClient dockerClient,
        IRepositoryService repositoryService,
        ILogger<ArtifactScanner> logger)
    {
        _deployableArtifactsService = deployableArtifactsService;
        _layerService = layerService;
        _dockerClient = dockerClient;
        _repositoryService = repositoryService;
        _logger = logger;
    }

    public async Task<ArtifactScannerResult> ScanImage(string repo, string tag, CancellationToken cancellationToken)
    {
        var manifest = await _dockerClient.LoadManifest(repo, tag);
        if (manifest == null) throw new Exception($"Failed to load manifest for {repo}:{tag}");

        _logger.LogInformation("Downloading manifest for {Repo}:{Tag}...", repo, tag);

        // Extract the labels.
        var image = await _dockerClient.LoadManifestImage(repo, manifest.config);
        var labels = new Dictionary<string, string>();
        if (image != null) labels = image.config.Labels;


        var isService = labels.TryGetValue("defra.cdp.service.name", out var serviceName);
        var isTestSuite = labels.TryGetValue("defra.cdp.testsuite.name", out var testName);

        if (!isService && !isTestSuite)
            return ArtifactScannerResult.Failure($"Not an CDP service or test suite, image {repo}:{tag} is missing label defra.cdp.service.name or defra.cdp.testsuite.name");

        _logger.LogInformation("Scanning layers in {Repo}:{Tag} for package.json...", repo, tag);

        // Search all the layers for files of interest.
        var searchLayerTasks = manifest.layers.Select(blob => SearchLayerUsingCache(repo, blob, cancellationToken));
        var searchLayerResults = await Task.WhenAll(searchLayerTasks);


        // Flatten file list. Files in higher layers should overwrite ones in lower layers.
        // TODO: handle docker whiteout files.
        var mergedFiles = FlattenFiles(searchLayerResults);

        labels.TryGetValue("defra.cdp.git.repo.url", out var githubUrl);


        var runMode = ArtifactRunMode.Service;
        if (labels.TryGetValue("defra.cdp.run_mode", out var sRunMode))
        {
            Enum.TryParse(sRunMode, true, out runMode);
        }

        long semver = 0;

        try
        {
            semver = SemVer.SemVerAsLong(tag);
        }
        catch (Exception ex)
        {
            return ArtifactScannerResult.Failure($"Invalid semver tag {repo}:{tag} - {ex.Message}");
        }

        var repository = await _repositoryService.FindRepositoryById(repo, cancellationToken);
        // Persist the results.
        var artifact = new DeployableArtifact
        {
            ScannerVersion = DockerScannerVersion,
            Repo = repo,
            Tag = tag,
            Sha256 = manifest.digest!,
            GithubUrl = githubUrl,
            ServiceName = serviceName ?? testName,
            Files = mergedFiles.Values.ToList(),
            SemVer = semver,
            Teams = repository?.Teams ?? new List<RepositoryTeam>(),
            RunMode = runMode.ToString().ToLower()
        };

        _logger.LogInformation("Saving artifact {Repo}:{Tag}...", repo, tag);
        await _deployableArtifactsService.CreateAsync(artifact, cancellationToken);

        _logger.LogInformation("Artifact {Repo}:{Tag} completed", repo, tag);
        return new ArtifactScannerResult(artifact);
    }

    // The new backfill just generates a list of curl commands we can run from the terminal to trigger the backfill
    public async Task<List<RescanRequest>> Backfill(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating backfill script");

        var catalog = await _dockerClient.LoadCatalog(cancellationToken);

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


    private async Task<Layer> SearchLayerUsingCache(string repo, Blob blob, CancellationToken cancellationToken)
    {
        var layer = await _layerService.Find(blob.digest, cancellationToken);
        if (layer == null)
        {
            _logger.LogDebug("Layer {BlobDigest} not in cache, scanning...", blob.digest);
            layer = await _dockerClient.SearchLayer(repo, blob, _filesToExtract, _pathsToIgnore);
            await _layerService.CreateAsync(layer, cancellationToken);
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