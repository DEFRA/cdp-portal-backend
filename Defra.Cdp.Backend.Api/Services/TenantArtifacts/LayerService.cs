using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TenantArtifacts;

public interface ILayerService
{
    Task CreateAsync(Layer layer, CancellationToken cancellationToken);
    Task<Layer?> Find(string digest, CancellationToken cancellationToken);
    Task<LayerFile?> FindFileAsync(string layerDigest, string fileName, CancellationToken cancellationToken);
}

public sealed class LayerService : MongoService<Layer>, ILayerService
{
    private const string CollectionName = "layers";

    public LayerService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task<Layer?> Find(string digest, CancellationToken cancellationToken)
    {
        return await Collection.Find(l => l.Digest == digest).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<LayerFile?> FindFileAsync(string layerDigest, string path, CancellationToken cancellationToken)
    {
        var layer = await Collection.Find(l => l.Digest == layerDigest).FirstAsync(cancellationToken);
        return layer?.Files.Find(lf => lf.FileName == path);
    }

    // TODO: Transform this into a binary where we can store it as compressed or binary with MongoDB
    // explore the options and see what's the best
    // We could also use the digest hash from image on dockerhub 
    public async Task CreateAsync(Layer layer, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(layer, cancellationToken: cancellationToken);
    }

    protected override List<CreateIndexModel<Layer>> DefineIndexes(IndexKeysDefinitionBuilder<Layer> builder)
    {
        var indexDigest = new CreateIndexModel<Layer>(builder.Ascending(l => l.Digest));
        return new List<CreateIndexModel<Layer>> { indexDigest };
    }
}