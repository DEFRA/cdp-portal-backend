using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDeployablesService
{
    Task CreateAsync(DeployableArtifact artifact);
    Task CreatePlaceholderAsync(string serviceName, string githubUrl);
    Task<DeployableArtifact?> FindByTag(string repo, string tag);
    Task<List<DeployableArtifact>> FindAll();

    Task UpdateAll(List<Repository> repositories,
        CancellationToken cancellationToken); // TODO: remove once we migrated old deployable artifacts

    Task<List<DeployableArtifact>> FindAll(string repo);
    Task<List<string>> FindAllRepoNames();
    Task<List<string>> FindAllRepoNames(IEnumerable<string> groups);
    Task<List<ServiceInfo>> FindAllServices();
    Task<List<string>> FindAllTagsForRepo(string repo);
    Task<List<string>> FindAllTagsForRepo(string repo, IEnumerable<string> groups);
    Task<ServiceInfo?> FindServices(string service);
}

public class DeployablesService : MongoService<DeployableArtifact>, IDeployablesService
{
    private const string CollectionName = "artifacts";

    public DeployablesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory, CollectionName, loggerFactory)
    {
    }

    public async Task CreateAsync(DeployableArtifact artifact)
    {
        await Collection.ReplaceOneAsync(
            a => a.Repo == artifact.Repo && a.Tag == artifact.Tag,
            artifact,
            new ReplaceOptions { IsUpsert = true }
        );
    }

    public async Task CreatePlaceholderAsync(string serviceName, string githubUrl)
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
            Teams = new List<RepositoryTeam>()
        };

        await Collection.ReplaceOneAsync(
            a => a.Repo == artifact.Repo && a.Tag == artifact.Tag,
            artifact,
            new ReplaceOptions { IsUpsert = true }
        );
    }


    public async Task<DeployableArtifact?> FindByTag(string repo, string tag)
    {
        return await Collection.Find(d => d.Repo == repo && d.Tag == tag).FirstOrDefaultAsync();
    }

    public async Task<List<DeployableArtifact>> FindAll()
    {
        return await Collection.Find(FilterDefinition<DeployableArtifact>.Empty).ToListAsync();
    }

    public async Task<List<DeployableArtifact>> FindAll(string repo)
    {
        return await Collection.Find(a => a.Repo == repo).ToListAsync();
    }

    public async Task<List<string>> FindAllRepoNames()
    {
        return await Collection
            .Distinct(d => d.Repo, FilterDefinition<DeployableArtifact>.Empty)
            .ToListAsync();
    }

    public async Task<List<string>> FindAllRepoNames(IEnumerable<string> groups)
    {
        return await Collection
            .Distinct(d => d.Repo, d => d.Teams.Any(t => groups.Contains(t.TeamId)))
            .ToListAsync();
    }

    public async Task<List<string>> FindAllTagsForRepo(string repo)
    {
        var res = await Collection
            .Find(d => d.Repo == repo)
            .ToListAsync();
        return res.Select(d => d.Tag).Where(SemVer.IsSemVer).ToList();
    }

    public async Task<List<string>> FindAllTagsForRepo(string repo, IEnumerable<string> groups)
    {
        var res = await Collection
            .Find(d => d.Repo == repo && d.Teams.Any(t => groups.Contains(t.TeamId)))
            .ToListAsync();
        return res.Select(d => d.Tag).Where(SemVer.IsSemVer).ToList();
    }

    public async Task<ServiceInfo?> FindServices(string service)
    {
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Match(d => d.ServiceName == service)
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo, grp.Root.Teams))
            .Limit(1);
        return await Collection.Aggregate(pipeline).FirstOrDefaultAsync();
    }


    public async Task<List<ServiceInfo>> FindAllServices()
    {
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo, grp.Root.Teams));

        var result = await Collection.Aggregate(pipeline).ToListAsync() ?? new List<ServiceInfo>();

        return result;
    }

    // TODO: remove once we migrated old deployable artifacts
    public async Task UpdateAll(List<Repository> repositories, CancellationToken cancellationToken)
    {
        var allDeployables = await FindAll();
        var serviceNames = allDeployables.Select(d => d.ServiceName).Where(d => d != null);
        var filteredRepositories = repositories.Where(r => serviceNames.Contains(r.Id));
        var newDeployableArtifacts = allDeployables.Select(d => new DeployableArtifact
        {
            Id = d.Id,
            Created = d.Created,
            Repo = d.Repo,
            Tag = d.Tag,
            Sha256 = d.Sha256,
            GithubUrl = d.GithubUrl,
            ServiceName = d.ServiceName,
            ScannerVersion = d.ScannerVersion,
            Teams = filteredRepositories.FirstOrDefault(r => r.Id == d.ServiceName)?.Teams ?? new List<RepositoryTeam>(),
            Files = d.Files,
            SemVer = d.SemVer
        });
        var replaceOneModels =
            newDeployableArtifacts.Select(newDeployableArtifact =>
            {
                var filter = Builders<DeployableArtifact>.Filter
                    .Eq(d => d.Id, newDeployableArtifact.Id);
                return new ReplaceOneModel<DeployableArtifact>(filter, newDeployableArtifact) { IsUpsert = true };
            }).ToList();

        if (replaceOneModels.Any())
        {
            await Collection.BulkWriteAsync(replaceOneModels, new BulkWriteOptions(), cancellationToken);    
        }
    }

    public async Task<List<string?>> FindAllUniqueGithubRepos()
    {
        return await Collection.Distinct(d => d.GithubUrl, FilterDefinition<DeployableArtifact>.Empty)
            .ToListAsync();
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