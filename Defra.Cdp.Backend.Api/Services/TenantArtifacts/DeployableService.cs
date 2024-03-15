using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDeployablesService
{
    Task CreateAsync(DeployableArtifact artifact, CancellationToken cancellationToken);

    Task CreatePlaceholderAsync(string serviceName, string githubUrl, ArtifactRunMode runMode,
        CancellationToken cancellationToken);

    Task<List<string>> DeployableEnvironments(bool isAdmin);

    Task<List<DeployableArtifact>> FindAll(CancellationToken cancellationToken);
    Task<List<DeployableArtifact>> FindAll(string repo, CancellationToken cancellationToken);

    Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, CancellationToken cancellationToken);

    Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, IEnumerable<string> groups,
        CancellationToken cancellationToken);

    Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken);
    Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken);

    Task<List<TagInfo>> FindAllTagsForRepo(string repo, CancellationToken cancellationToken);

    Task<List<ServiceInfo>> FindAllServices(ArtifactRunMode? runMode, CancellationToken cancellationToken);
    Task<ServiceInfo?> FindServices(string service, CancellationToken cancellationToken);
}

public class DeployablesService : MongoService<DeployableArtifact>, IDeployablesService
{
    private const string CollectionName = "artifacts";
    private readonly List<string> _adminEnvironments;
    private readonly List<string> _allEnvironments;
    private readonly List<string> _tenantEnvironments;

    public DeployablesService(IMongoDbClientFactory connectionFactory, IConfiguration configuration,
        ILoggerFactory loggerFactory) : base(
        connectionFactory, CollectionName, loggerFactory)
    {
        _adminEnvironments = configuration.GetSection("DeploymentEnvironments:Admin").Get<List<string>>()!;
        _tenantEnvironments = configuration.GetSection("DeploymentEnvironments:Tenants").Get<List<string>>()!;
        _allEnvironments = _adminEnvironments.Concat(_tenantEnvironments).Distinct().ToList();
    }


    public async Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo && d.Tag == tag).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo).SortByDescending(d => d.SemVer)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<DeployableArtifact>> FindAll(CancellationToken cancellationToken)
    {
        return await Collection.Find(FilterDefinition<DeployableArtifact>.Empty).ToListAsync(cancellationToken);
    }

    public Task<List<string>> DeployableEnvironments(bool isAdmin)
    {
        return Task.FromResult(isAdmin ? _allEnvironments : _tenantEnvironments);
    }

    public async Task<List<DeployableArtifact>> FindAll(string repo, CancellationToken cancellationToken)
    {
        return await Collection.Find(a => a.Repo == repo).ToListAsync(cancellationToken);
    }

    public async Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, CancellationToken cancellationToken)
    {
        return await Collection
            .Distinct(d => d.Repo, d => d.RunMode == runMode.ToString().ToLower())
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, IEnumerable<string> groups,
        CancellationToken cancellationToken)
    {
        return await Collection
            .Distinct(d => d.Repo,
                d => d.RunMode == runMode.ToString().ToLower() && d.Teams.Any(t => groups.Contains(t.TeamId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TagInfo>> FindAllTagsForRepo(string repo, CancellationToken cancellationToken)
    {
        var sort = Builders<DeployableArtifact>.Sort.Descending(d => d.SemVer);
        var res = await Collection
            .Find(d => d.Repo == repo)
            .Project(d => new TagInfo(d.Tag, d.Created))
            .Sort(sort)
            .ToListAsync(cancellationToken);

        return res.Where(t => SemVer.IsSemVer(t.Tag)).ToList();
    }

    public async Task CreateAsync(DeployableArtifact artifact, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(
            a => a.Repo == artifact.Repo && a.Tag == artifact.Tag,
            artifact,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public async Task<ServiceInfo?> FindServices(string service, CancellationToken cancellationToken)
    {
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Match(d => d.ServiceName == service)
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo, grp.Root.Teams))
            .Limit(1);
        return await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ServiceInfo>> FindAllServices(ArtifactRunMode? runMode, CancellationToken cancellationToken)
    {
        var fd = new FilterDefinitionBuilder<DeployableArtifact>();
        var filter = fd.Empty;
        if (runMode != null) filter = fd.Eq(d => d.RunMode, runMode.ToString()?.ToLower());

        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Match(filter)
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo, grp.Root.Teams));

        var result = await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
                         .ToListAsync(cancellationToken) ??
                     new List<ServiceInfo>();

        return result;
    }

    public async Task CreatePlaceholderAsync(string serviceName, string githubUrl, ArtifactRunMode runMode,
        CancellationToken cancellationToken)
    {
        var artifact = new DeployableArtifact
        {
            Created = DateTime.UtcNow,
            Files = new List<DeployableArtifactFile>(),
            GithubUrl = githubUrl,
            Id = null,
            Repo = serviceName,
            Sha256 = "",
            Tag = "0.0.0",
            ScannerVersion = 1,
            SemVer = 0,
            ServiceName = serviceName,
            Teams = new List<RepositoryTeam>(),
            RunMode = runMode.ToString().ToLower()
        };

        await Collection.ReplaceOneAsync(
            a => a.Repo == artifact.Repo && a.Tag == artifact.Tag,
            artifact,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public async Task<List<string?>> FindAllUniqueGithubRepos(CancellationToken cancellationToken)
    {
        return await Collection
            .Distinct(d => d.GithubUrl, FilterDefinition<DeployableArtifact>.Empty,
                cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<DeployableArtifact>> DefineIndexes(
        IndexKeysDefinitionBuilder<DeployableArtifact> builder)
    {
        var githubUrlIndex = new CreateIndexModel<DeployableArtifact>(builder.Ascending(r => r.GithubUrl));
        var repoAndTagIndex =
            new CreateIndexModel<DeployableArtifact>(builder.Combine(builder.Ascending(r => r.Repo),
                builder.Ascending(r => r.Tag)));
        var hashIndex = new CreateIndexModel<DeployableArtifact>(builder.Ascending(r => r.Sha256));
        var teamIdIndex = new CreateIndexModel<DeployableArtifact>(
            builder.Ascending(d => d.Teams.Select(t => t.TeamId)),
            new CreateIndexOptions { Sparse = true });
        return new List<CreateIndexModel<DeployableArtifact>>
        {
            githubUrlIndex, repoAndTagIndex, hashIndex, teamIdIndex
        };
    }
}