using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.FeatureToggles;

public interface IFeatureTogglesService
{
    Task CreateToggle(FeatureToggle featureToggle, CancellationToken cancellationToken);
    Task<List<FeatureToggle>> GetAllToggles(CancellationToken cancellationToken);
    Task<FeatureToggle?> GetToggle(string toggleId, CancellationToken cancellationToken);
    Task<bool> IsAnyToggleActiveForPath(string requestPath, CancellationToken cancellationToken);
    Task UpdateToggle(string featureToggleId, bool isActive, CancellationToken cancellationToken);
}

public class FeatureTogglesService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<FeatureToggle>(connectionFactory,
        CollectionName, loggerFactory), IFeatureTogglesService
{
    private const string CollectionName = "featuretoggles";

    protected override List<CreateIndexModel<FeatureToggle>> DefineIndexes(
        IndexKeysDefinitionBuilder<FeatureToggle> builder)
    {
        var bodyIndex = new CreateIndexModel<FeatureToggle>(builder.Text(r => r.Id));
        return [bodyIndex];
    }

    public async Task CreateToggle(FeatureToggle featureToggle, CancellationToken cancellationToken)
    {
        var filter = Builders<FeatureToggle>.Filter.Eq(e => e.Id, featureToggle.Id);

        await Collection.ReplaceOneAsync(
            filter,
            featureToggle,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public async Task<List<FeatureToggle>> GetAllToggles(CancellationToken cancellationToken)
    {
        return await Collection.Find(_ => true)
            .ToListAsync(cancellationToken);
    }

    public async Task<FeatureToggle?> GetToggle(string toggleId, CancellationToken cancellationToken)
    {
        return await Collection.Find(toggle => toggle.Id == toggleId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsAnyToggleActiveForPath(string requestPath, CancellationToken cancellationToken)
    {
        var toggles = await Collection
            .Find(toggle => toggle.Active)
            .Project(toggle => toggle.Url)
            .ToListAsync(cancellationToken);

        return toggles.Any(url => requestPath.StartsWith(url, StringComparison.OrdinalIgnoreCase));

    }

    public async Task UpdateToggle(string featureToggleId, bool isActive, CancellationToken cancellationToken)
    {
        var filter = Builders<FeatureToggle>.Filter.Eq(e => e.Id, featureToggleId);

        var update = Builders<FeatureToggle>.Update.Set(ft => ft.Active, isActive);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}

public class FeatureToggle
{
    [property: JsonPropertyName("id")]
    public string Id { get; init; } = default;

    [property: JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [property: JsonPropertyName("url")]
    public string Url { get; init; } = default!;

    [property: JsonPropertyName("active")]
    public bool Active { get; init; } = default!;
}