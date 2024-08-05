using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

public interface IPendingSecretsService
{
    public Task RegisterPendingSecret(RegisterPendingSecret registerPendingSecret, CancellationToken cancellationToken);

    public Task<PendingSecret?> ExtractPendingSecret(string environment, string service, string secretKey,
        string action, CancellationToken cancellationToken);

    public Task<PendingSecrets?> FindPendingSecrets(string environment, string service,
        CancellationToken cancellationToken);

    public Task AddException(string environment, string service, string secretKey,
        string action, string exceptionMessage, CancellationToken cancellationToken);

    public Task<string?> PullExceptionMessage(string environment, string service, CancellationToken cancellationToken);
}

public class PendingSecretsService : MongoService<PendingSecrets>, IPendingSecretsService
{
    private readonly ILogger<PendingSecretsService> _logger;

    public PendingSecretsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory, "pendingsecrets", loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PendingSecretsService>();
    }

    protected override List<CreateIndexModel<PendingSecrets>> DefineIndexes(
        IndexKeysDefinitionBuilder<PendingSecrets> builder)
    {
        var serviceAndEnv = new CreateIndexModel<PendingSecrets>(builder.Combine(
            builder.Ascending(p => p.Service),
            builder.Ascending(p => p.Environment)
        ));

        var ttlIndex = new CreateIndexModel<PendingSecrets>(
            builder.Ascending(p => p.CreatedAt),
            new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.FromMinutes(2)
            }
        );

        return new List<CreateIndexModel<PendingSecrets>> { serviceAndEnv, ttlIndex };
    }

    public async Task RegisterPendingSecret(
        RegisterPendingSecret registerPendingSecret,
        CancellationToken cancellationToken
    )
    {
        var fb = new FilterDefinitionBuilder<PendingSecrets>();

        var filter = fb.And(
            fb.Eq(p => p.Service, registerPendingSecret.Service),
            fb.Eq(p => p.Environment, registerPendingSecret.Environment)
        );

        var update = Builders<PendingSecrets>
            .Update
            .Set(p => p.CreatedAt, DateTime.UtcNow)
            .Push(p => p.Pending,
                new PendingSecret
                {
                    SecretKey = registerPendingSecret.SecretKey, Action = registerPendingSecret.Action
                });

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<PendingSecret?> ExtractPendingSecret(string environment, string service, string secretKey,
        string action, CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<PendingSecrets>();

        var filter = fb.And(
            fb.Eq(p => p.Service, service),
            fb.Eq(p => p.Environment, environment)
        );

        var update = Builders<PendingSecrets>
            .Update
            .PullFilter(p => p.Pending, s => s.Action == action && s.SecretKey == secretKey);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<PendingSecrets> { ReturnDocument = ReturnDocument.Before }, cancellationToken);

        return result?.Pending.FirstOrDefault(p => p.Action == action && p.SecretKey == secretKey);
    }

    public async Task<PendingSecrets?> FindPendingSecrets(string environment, string service,
        CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<PendingSecrets>();

        var filter = fb.And(
            fb.Eq(p => p.Service, service),
            fb.Eq(p => p.Environment, environment)
        );


        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddException(
        string environment, string service, string secretKey,
        string action, string exceptionMessage, CancellationToken cancellationToken
    )
    {
        var pendingSecret = await ExtractPendingSecret(environment, service, secretKey, action, cancellationToken);

        if (pendingSecret != null)
        {
            var fb = new FilterDefinitionBuilder<PendingSecrets>();
            var filter = fb.And(
                fb.Eq(p => p.Service, service),
                fb.Eq(p => p.Environment, environment)
            );

            var update = Builders<PendingSecrets>
                .Update
                .Set(p => p.CreatedAt, DateTime.UtcNow)
                .Push(p => p.ExceptionMessages, exceptionMessage);

            await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Add Exception: Secret {SecretKey} not found in pending secrets", secretKey);
        }
    }

    public async Task<string?> PullExceptionMessage(string environment, string service,
        CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<PendingSecrets>();

        var filter = fb.And(
            fb.Eq(p => p.Service, service),
            fb.Eq(p => p.Environment, environment)
        );

        var update = Builders<PendingSecrets>
            .Update
            .PopFirst(p => p.ExceptionMessages);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<PendingSecrets> { ReturnDocument = ReturnDocument.Before }, cancellationToken);

        return result?.ExceptionMessages.FirstOrDefault();
    }
}