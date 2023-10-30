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
    Task<List<DeployableArtifact>> FindAll(string repo);
    Task<List<string>> FindAllRepoNames();
    Task<List<ServiceInfo>> FindAllServices();
    Task<List<string>> FindAllTagsForRepo(string repo);
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
            ServiceName = serviceName
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

    public async Task<List<string>> FindAllTagsForRepo(string repo)
    {
        var res = await Collection
            .Find(d => d.Repo == repo)
            .ToListAsync();
        // TODO: fix this up once we understand why documentdb doesn't like projects 
        return res.Select(d => d.Tag).Where(SemVer.IsSemVer).ToList();
    }

    public async Task<ServiceInfo?> FindServices(string service)
    {
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Match(d => d.ServiceName == service)
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo)).Limit(1);
        return await Collection.Aggregate(pipeline).FirstOrDefaultAsync();
    }


    public async Task<List<ServiceInfo>> FindAllServices()
    {
        var pipeline = new EmptyPipelineDefinition<DeployableArtifact>()
            .Group(d => d.ServiceName,
                grp => new { DeployedAt = grp.Max(d => d.Created), Root = grp.Last() })
            .Project(grp => new ServiceInfo(grp.Root.ServiceName!, grp.Root.GithubUrl, grp.Root.Repo));

        var result = await Collection.Aggregate(pipeline).ToListAsync() ?? new List<ServiceInfo>();

        return result;
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
        return new List<CreateIndexModel<DeployableArtifact>> { githubUrlIndex, repoAndTagIndex, hashIndex };
    }
}