using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;


[Obsolete("Use EntityService")]
public interface IVanityUrlsService
{
    Task<List<VanityUrlRecord>> FindService(string service, CancellationToken cancellationToken);

    Task<List<VanityUrlRecord>> FindServiceByEnv(string service, string environment,
        CancellationToken cancellationToken);

    Task<ShutterableUrl?> FindByUrl(string url, CancellationToken cancellationToken);
}

/**
 * Combines data from NginxVanityUrls, EnabledVanityUrls and Shuttering
 */

[Obsolete("Use EntityService")]
public class VanityUrlsService(IMongoDbClientFactory connectionFactory) : IVanityUrlsService
{
    public async Task<List<VanityUrlRecord>> FindAll(CancellationToken cancellationToken)
    {
        var collection = connectionFactory.GetCollection<NginxVanityUrlsRecord>(NginxVanityUrlsService.CollectionName);
        return await collection.Aggregate<VanityUrlRecord>(_pipeline).ToListAsync(cancellationToken);
    }

    public async Task<List<VanityUrlRecord>> FindService(string service, CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument("serviceName", service));
        return await Find(matchStage, cancellationToken);
    }

    public async Task<List<VanityUrlRecord>> FindEnv(string environment, CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument("environment", environment));
        return await Find(matchStage, cancellationToken);
    }

    public async Task<List<VanityUrlRecord>> FindServiceByEnv(string service, string environment,
        CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match",
            new BsonDocument { { "serviceName", service }, { "environment", environment } });
        return await Find(matchStage, cancellationToken);
    }

    public async Task<ShutterableUrl?> FindByUrl(string url, CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument("url", url));
        var records = await Find(matchStage, cancellationToken);
        return records.FirstOrDefault()?.ToShutterableUrl();
    }

    private async Task<List<VanityUrlRecord>> Find(BsonDocument matchStage, CancellationToken cancellationToken)
    {
        var collection = connectionFactory.GetCollection<NginxVanityUrlsRecord>(NginxVanityUrlsService.CollectionName);
        return await collection.Aggregate<VanityUrlRecord>(_pipeline.Prepend(matchStage).ToArray())
            .ToListAsync(cancellationToken);
    }


    private readonly BsonDocument[] _pipeline =
    [
        new("$lookup",
            new BsonDocument
            {
                { "from", ShutteredUrlsService.CollectionName },
                { "localField", "url" },
                { "foreignField", "url" },
                { "as", "shutteredData" }
            }),
        new("$lookup",
            new BsonDocument
            {
                { "from", EnabledVanityUrlsService.CollectionName },
                { "localField", "url" },
                { "foreignField", "url" },
                { "as", "enabledData" }
            }),
        new("$project",
            new BsonDocument
            {
                { "url", 1 },
                { "environment", 1 },
                { "serviceName", 1 },
                {
                    "shuttered", new BsonDocument
                    {
                        {
                            "$gt", new BsonArray
                            {
                                new BsonDocument("$size",
                                    new BsonDocument("$ifNull",
                                        new BsonArray { "$shutteredData", new BsonArray() })),
                                0
                            }
                        }
                    }
                },
                {
                    "enabled", new BsonDocument
                    {
                        {
                            "$gt", new BsonArray
                            {
                                new BsonDocument("$size",
                                    new BsonDocument("$ifNull",
                                        new BsonArray { "$enabledData", new BsonArray() })),
                                0
                            }
                        }
                    }
                }
            })
    ];
}

[BsonIgnoreExtraElements]
public record VanityUrlRecord(string Url, string Environment, string ServiceName, bool Enabled, bool Shuttered)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;


    public ShutterableUrl ToShutterableUrl()
    {
        return new ShutterableUrl(ServiceName, Environment, Url, Enabled, Shuttered, true);
    }
}