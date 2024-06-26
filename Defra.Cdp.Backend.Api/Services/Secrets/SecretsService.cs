using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface ISecretsService
{
    public Task UpdateSecrets(TenantSecrets secret, CancellationToken cancellationToken);
    public Task UpdateSecrets(List<TenantSecrets> secrets, CancellationToken cancellationToken);
    public Task<TenantSecrets?> FindSecrets( string environment, string service, CancellationToken cancellationToken);
}

public class SecretsService : MongoService<TenantSecrets>, ISecretsService
{
    
    public SecretsService(IMongoDbClientFactory connectionFactory,ILoggerFactory loggerFactory) : base(connectionFactory, "tenantsecrets", loggerFactory)
    {

    }

    protected override List<CreateIndexModel<TenantSecrets>> DefineIndexes(IndexKeysDefinitionBuilder<TenantSecrets> builder)
    {
        var serviceAndEnv = new CreateIndexModel<TenantSecrets>(builder.Combine(
            builder.Ascending(s => s.Service),
            builder.Ascending(s => s.Environment)
        ));

        return new List<CreateIndexModel<TenantSecrets>> { serviceAndEnv};
    }

    public async Task<TenantSecrets?> FindSecrets(string environment, string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(t => t.Service == service && t.Environment == environment).FirstOrDefaultAsync(cancellationToken);
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
        {
            await Collection.BulkWriteAsync(updateSecretModels, new BulkWriteOptions(), cancellationToken);
        }
    }

}