using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface IDeployableArtifactsService
{
    Task CreateAsync(DeployableArtifact artifact, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(string service, string tag, CancellationToken cancellationToken);

    Task<List<DeployableArtifact>> FindAll(string repo, CancellationToken cancellationToken);

    Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken);

    Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken);
    
    Task<List<ArtifactVersion>> FindLatestForAll(CancellationToken cancellationToken);

    Task<List<TagInfo>> FindAllTagsForRepo(string repo, CancellationToken cancellationToken);
}

public class DeployableArtifactsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<DeployableArtifact>(connectionFactory, CollectionName, loggerFactory), IDeployableArtifactsService
{
    private const string CollectionName = "artifacts";

    public async Task<DeployableArtifact?> FindByTag(string repo, string tag, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo && d.Tag == tag).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeployableArtifact?> FindLatest(string repo, CancellationToken cancellationToken)
    {
        return await Collection.Find(d => d.Repo == repo).SortByDescending(d => d.SemVer)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ArtifactVersion>> FindLatestForAll(CancellationToken cancellationToken)
    {
        return await Collection.Aggregate()
            .SortBy(a => a.Repo)
            .ThenByDescending(a => a.SemVer)
            .Group(a => a.Repo, g => new ArtifactVersion(g.Key, g.First().Tag))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(string service, string tag, CancellationToken cancellationToken)
    {
        var result = await Collection.DeleteOneAsync(d => d.Repo == service && d.Tag == tag, cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<List<DeployableArtifact>> FindAll(string repo, CancellationToken cancellationToken)
    {
        return await Collection.Find(a => a.Repo == repo).ToListAsync(cancellationToken);
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
    
    protected override List<CreateIndexModel<DeployableArtifact>> DefineIndexes(
        IndexKeysDefinitionBuilder<DeployableArtifact> builder)
    {
        var repoAndTagIndex =
            new CreateIndexModel<DeployableArtifact>(builder.Combine(builder.Ascending(r => r.Repo),
                builder.Ascending(r => r.Tag)));
        var hashIndex = new CreateIndexModel<DeployableArtifact>(builder.Ascending(r => r.Sha256));
        return [repoAndTagIndex, hashIndex];
    }
}