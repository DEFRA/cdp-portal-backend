using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface ISecretsService
{
    public Task UpdateSecrets(TenantSecrets secret, CancellationToken cancellationToken);
    public Task UpdateSecrets(List<TenantSecrets> secrets, CancellationToken cancellationToken);
    public Task DeleteSecrets(List<TenantSecrets> secrets, CancellationToken cancellationToken);

    public Task<TenantSecrets?> FindServiceSecretsForEnvironment(string environment, string service,
        CancellationToken cancellationToken);

    public Task<Dictionary<string, TenantSecretKeys>> FindAllServiceSecrets(string service,
        CancellationToken cancellationToken);

    public Task<List<TenantSecrets>> FindAllSecretsForEnvironment(string environment,
        CancellationToken cancellationToken);

    public Task AddSecretKey(string environment, string service, string secretKey, CancellationToken cancellationToken);
}

public class SecretsService : MongoService<TenantSecrets>, ISecretsService
{
    public SecretsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory, "tenantsecrets", loggerFactory)
    {
    }

    public async Task<TenantSecrets?> FindServiceSecretsForEnvironment(string environment, string service,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.Service == service && t.Environment == environment)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<string, TenantSecretKeys>> FindAllServiceSecrets(string service,
        CancellationToken cancellationToken)
    {
        var allServiceSecrets = await Collection.Find(t => t.Service == service)
            .ToListAsync(cancellationToken);

        return allServiceSecrets
            .GroupBy(s => s.Environment)
            .ToDictionary(g => g.Key,
                g =>
                {
                    var tenantSecret = g.First();
                    tenantSecret.Keys.Sort();
                    return tenantSecret.AsTenantSecretKeys();
                });
    }

    public async Task<List<TenantSecrets>> FindAllSecretsForEnvironment(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.Environment == environment)
            .ToListAsync(cancellationToken);

    }

    public async Task UpdateSecrets(TenantSecrets secret, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(
            s => s.Service == secret.Service && s.Environment == secret.Environment, secret,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task UpdateSecrets(List<TenantSecrets> secrets, CancellationToken cancellationToken)
    {
        var updateSecretModels =
            secrets.Select(secret =>
            {
                var filterBuilder = Builders<TenantSecrets>.Filter;
                var filter = filterBuilder
                    .And(
                        filterBuilder.Eq(s => s.Service, secret.Service),
                        filterBuilder.Eq(s => s.Environment, secret.Environment)
                    );
                return new ReplaceOneModel<TenantSecrets>(filter, secret) { IsUpsert = true };
            }).ToList();

        if (updateSecretModels.Any())
            await Collection.BulkWriteAsync(updateSecretModels, new BulkWriteOptions(), cancellationToken);
    }
    
    public async Task DeleteSecrets(List<TenantSecrets> secrets, CancellationToken cancellationToken)
    {
        var filter = Builders<TenantSecrets>.Filter.In("_id", secrets.Select(v => v.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }


    public async Task AddSecretKey(string environment, string service, string secretKey,
        CancellationToken cancellationToken
    )
    {
        var fb = new FilterDefinitionBuilder<TenantSecrets>();

        var filter = fb.And(
            fb.Eq(s => s.Service, service),
            fb.Eq(s => s.Environment, environment)
        );

        var update = Builders<TenantSecrets>
            .Update
            .Set(s => s.LastChangedDate, DateTime.UtcNow.ToString("o"))
            .AddToSet(s => s.Keys, secretKey);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    protected override List<CreateIndexModel<TenantSecrets>> DefineIndexes(
        IndexKeysDefinitionBuilder<TenantSecrets> builder)
    {
        var serviceAndEnv = new CreateIndexModel<TenantSecrets>(builder.Combine(
            builder.Ascending(s => s.Service),
            builder.Ascending(s => s.Environment)
        ));

        return new List<CreateIndexModel<TenantSecrets>> { serviceAndEnv };
    }
}