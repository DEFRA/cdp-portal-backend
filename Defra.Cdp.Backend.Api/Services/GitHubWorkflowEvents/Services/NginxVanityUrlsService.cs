using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

public interface INginxVanityUrlsService : IEventsPersistenceService<NginxVanityUrlsPayload>;

public class NginxVanityUrlsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<NginxVanityUrlsRecord>(connectionFactory,
        CollectionName, loggerFactory), INginxVanityUrlsService
{
    public const string CollectionName = "nginxvanityurls";
    private readonly ILogger<NginxVanityUrlsService> _logger = loggerFactory.CreateLogger<NginxVanityUrlsService>();

    public async Task<List<NginxVanityUrlsRecord>> FindVanityUrls(string service, string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.ServiceName == service && v.Environment == environment)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<List<NginxVanityUrlsRecord>> FindAllVanityUrls(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.ServiceName == service)
            .ToListAsync(cancellationToken);
    }

    public async Task PersistEvent(CommonEvent<NginxVanityUrlsPayload> workflowEvent, CancellationToken cancellationToken)
    {
        var payload = workflowEvent.Payload;
        _logger.LogInformation("Persisting vanity urls for environment: {Environment}", payload.Environment);

        var vanityUrls = new List<NginxVanityUrlsRecord>();
        foreach (var service in payload.Services)
        {
            vanityUrls.AddRange(
                service.Urls.Select(url => new NginxVanityUrlsRecord(payload.Environment, service.Name, $"{url.Host}.{url.Domain}"))
            );
        }

        var vanityUrlsInDb = await FindAllEnvironmentVanityUrls(payload.Environment, cancellationToken);
        
        var vanityUrlsToDelete = vanityUrlsInDb.ExceptBy(vanityUrls.Select(v => v.Url),
            v => v.Url).ToList();

        if (vanityUrlsToDelete.Count != 0)
        {
            await DeleteVanityUrls(vanityUrlsToDelete, cancellationToken);
        }
        
        if (vanityUrls.Count != 0)
        {
            await UpdateVanityUrls(vanityUrls, cancellationToken);
        }
    }

    protected override List<CreateIndexModel<NginxVanityUrlsRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<NginxVanityUrlsRecord> builder)
    {
        var envServiceName = new CreateIndexModel<NginxVanityUrlsRecord>(builder.Combine(
            builder.Descending(v => v.Environment),
            builder.Descending(v => v.ServiceName)
        ));

        var env = new CreateIndexModel<NginxVanityUrlsRecord>(
            builder.Descending(v => v.Environment)
        );
        
        var service = new CreateIndexModel<NginxVanityUrlsRecord>(
            builder.Descending(v => v.ServiceName)
        );
        return [env, service, envServiceName];
    }

    private async Task<List<NginxVanityUrlsRecord>> FindAllEnvironmentVanityUrls(string environment,
        CancellationToken cancellationToken)
    {
        return await Collection.Find(v => v.Environment == environment)
            .ToListAsync(cancellationToken);
    }

    private async Task DeleteVanityUrls(List<NginxVanityUrlsRecord> vanityUrlRecords, CancellationToken cancellationToken)
    {
        var filter = Builders<NginxVanityUrlsRecord>.Filter.In("_id", vanityUrlRecords.Select(v => v.Id));
        await Collection.DeleteManyAsync(filter, cancellationToken);
    }

    private async Task UpdateVanityUrls(List<NginxVanityUrlsRecord> vanityUrls,
        CancellationToken cancellationToken)
    {
        var updateVanityUrlsModels =
            vanityUrls.Select(vanityUrl =>
            {
                var filterBuilder = Builders<NginxVanityUrlsRecord>.Filter;
                var filter = filterBuilder.Where(v =>
                    v.ServiceName == vanityUrl.ServiceName && v.Environment == vanityUrl.Environment && v.Url == vanityUrl.Url);
                return new ReplaceOneModel<NginxVanityUrlsRecord>(filter, vanityUrl) { IsUpsert = true };
            }).ToList();

        await Collection.BulkWriteAsync(updateVanityUrlsModels, new BulkWriteOptions(), cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record NginxVanityUrlsRecord(string Environment, string ServiceName, string Url)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}