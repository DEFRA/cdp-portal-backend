using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Utils;
using Defra.Cdp.Backend.Api.Utils.Auditing;

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

public class ArtifactScanAndStore(
    IDeployableArtifactsService deployableArtifactsService,
    IDockerClient dockerClient,
    IRepositoryService repositoryService,
    ILogger<ArtifactScanAndStore> logger)
    : IArtifactScanner
{
    // Used to determine what would have been extracted. we might want to use this for rescans etc.
    private const int DockerScannerVersion = 1;

    private readonly ILogger _logger = logger;

    public async Task<ArtifactScannerResult> ScanImage(string repo, string tag, CancellationToken cancellationToken)
    {
        var manifest = await dockerClient.LoadManifest(repo, tag);
        if (manifest == null) throw new Exception($"Failed to load manifest for {repo}:{tag}");

        _logger.LogInformation("Downloading manifest for {Repo}:{Tag}...", repo, tag);

        // Extract the labels.
        var image = await dockerClient.LoadManifestImage(repo, manifest.config);
        var labels = new Dictionary<string, string>();
        if (image != null) labels = image.config.Labels;


        var isService = labels.TryGetValue("defra.cdp.service.name", out var serviceName);
        var isTestSuite = labels.TryGetValue("defra.cdp.testsuite.name", out var testName);

        if (!isService && !isTestSuite)
            return ArtifactScannerResult.Failure($"Not an CDP service or test suite, image {repo}:{tag} is missing label defra.cdp.service.name or defra.cdp.testsuite.name");

        labels.TryGetValue("defra.cdp.git.repo.url", out var githubUrl);

        var runMode = ArtifactRunMode.Service;
        if (labels.TryGetValue("defra.cdp.run_mode", out var sRunMode))
        {
            Enum.TryParse(sRunMode, true, out runMode);
        }

        long semver;

        // We expect tags to be a valid semantic version.
        // This use to be more important, but now it largely exists to support sorting by version cleanly.
        try
        {
            semver = SemVer.SemVerAsLong(tag);
        }
        catch (Exception ex)
        {
            return ArtifactScannerResult.Failure($"Invalid semver tag {repo}:{tag} - {ex.Message}");
        }
        
        var repository = await repositoryService.FindRepositoryById(repo, cancellationToken);

        var artifact = new DeployableArtifact
        {
            ScannerVersion = DockerScannerVersion,
            Created =  image?.created ?? DateTime.Now,
            Repo = repo,
            Tag = tag,
            Sha256 = manifest.digest!,
            GithubUrl = githubUrl,
            ServiceName = serviceName ?? testName,
            Files = [],
            SemVer = semver,
            Teams = repository?.Teams ?? [],
            RunMode = runMode.ToString().ToLower()
        };

        _logger.LogInformation("Saving artifact {Repo}:{Tag}...", repo, tag);
        await deployableArtifactsService.CreateAsync(artifact, cancellationToken);

        _logger.LogInformation("Artifact {Repo}:{Tag} completed", repo, tag);
        _logger.Audit("New artifact added to portal {Repo}:{Tag}", repo, tag);
        return new ArtifactScannerResult(artifact);
    }

    // The new backfill just generates a list of curl commands we can run from the terminal to trigger the backfill
    public async Task<List<RescanRequest>> Backfill(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating backfill script");

        var catalog = await dockerClient.LoadCatalog(cancellationToken);

        _logger.LogInformation("Backfill found {RepositoriesCount} repos to backfill", catalog.Repositories.Count);

        // get ALL the tags for all the repos
        var rescanRequests = new List<RescanRequest>();
        foreach (var catalogRepository in catalog.Repositories)
        {
            var tags = await dockerClient.FindTags(catalogRepository);
            foreach (var tag in tags.Tags)
            {
                _logger.LogInformation("Backfilling {CatalogRepository}:{Tag}", catalogRepository, tag);
                rescanRequests.Add(new RescanRequest(catalogRepository, tag));
            }
        }

        _logger.LogInformation("Backfill complete!");
        return rescanRequests;
    }
}