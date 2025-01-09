using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDeployableArtifactsService
{
    Task CreateAsync(DeployableArtifact artifact, CancellationToken cancellationToken);

    Task CreatePlaceholderAsync(string serviceName, string githubUrl, ArtifactRunMode runMode,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(string service, string tag, CancellationToken cancellationToken);

    Task<List<DeployableArtifact>> FindAll(CancellationToken cancellationToken);
    Task<List<DeployableArtifact>> FindAll(string repo, CancellationToken cancellationToken);

    Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, CancellationToken cancellationToken);

    Task<List<string>> FindAllRepoNames(ArtifactRunMode runMode, IEnumerable<string> groups,
        CancellationToken cancellationToken);

    Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken);
    Task<DeployableArtifact?> FindBySha256(string sha256, CancellationToken cancellationToken);
    Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken);

    Task<List<TagInfo>> FindAllTagsForRepo(string repo, CancellationToken cancellationToken);
    Task<List<TagInfo>> FindLatestTagsForRepo(string repo, int limit, CancellationToken cancellationToken);

    Task<List<ServiceInfo>> FindAllServices(ArtifactRunMode? runMode, string? teamId, string? service,
        CancellationToken cancellationToken);
    Task<ServiceInfo?> FindServices(string service, CancellationToken cancellationToken);

    Task<ServiceFilters> GetAllServicesFilters(CancellationToken ct);
    Task Decommission(string serviceName, CancellationToken cancellationToken);
}

public class DeployableArtifactsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<DeployableArtifact>(connectionFactory, CollectionName, loggerFactory), IDeployableArtifactsService
{
    private const string CollectionName = "artifacts";

    public async Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo && d.Tag == tag).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeployableArtifact?> FindBySha256(string sha256, CancellationToken cancellationToken)
    {
        var filter = Builders<DeployableArtifact>.Filter.Regex(d => d.Sha256, new BsonRegularExpression(sha256, "i"));
        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo).SortByDescending(d => d.SemVer)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(string service, string tag, CancellationToken cancellationToken)
    {
        var result = await Collection.DeleteOneAsync(d => d.Repo == service && d.Tag == tag, cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<List<DeployableArtifact>> FindAll(CancellationToken cancellationToken)
    {
        return await Collection.Find(FilterDefinition<DeployableArtifact>.Empty).ToListAsync(cancellationToken);
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

    public async Task<List<TagInfo>> FindLatestTagsForRepo(string repo, int limit, CancellationToken cancellationToken)
    {
        var sort = Builders<DeployableArtifact>.Sort.Descending(d => d.Created);
        return await Collection
            .Find(d => d.Repo == repo)
            .Project(d => new TagInfo(d.Tag, d.Created))
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(cancellationToken);
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

    public async Task Decommission(string serviceName, CancellationToken cancellationToken)
    {
        await Collection.DeleteManyAsync(da => da.ServiceName == serviceName, cancellationToken: cancellationToken);
    }

    public async Task<List<ServiceInfo>> FindAllServices(ArtifactRunMode? runMode, string? teamId, string? service,
        CancellationToken cancellationToken)
    {
        var builder = Builders<DeployableArtifact>.Filter;
        var filter = builder.Empty;

        if (runMode != null) filter = builder.Eq(d => d.RunMode, runMode.ToString()?.ToLower());

        if (teamId != null)
        {
            var teamFilter = builder.ElemMatch(d => d.Teams, t => t.TeamId == teamId);
            filter &= teamFilter;
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            var partialServiceFilter =
                builder.Regex(d => d.ServiceName,  new BsonRegularExpression(service, "i"));
            filter &= partialServiceFilter;
        }

        var sort = Builders<ServiceInfo>.Sort.Ascending(d => d.ServiceName);
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Match(filter)
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo, grp.Root.Teams))
            .Sort(sort);

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

    public async Task<ServiceFilters> GetAllServicesFilters(CancellationToken ct)
    {
        var serviceNames = await Collection
            .Distinct(d => d.ServiceName, d => d.RunMode == ArtifactRunMode.Service.ToString().ToLower(),
                cancellationToken: ct)
            .ToListAsync(ct);

        return new ServiceFilters { Services = serviceNames };
    }
}