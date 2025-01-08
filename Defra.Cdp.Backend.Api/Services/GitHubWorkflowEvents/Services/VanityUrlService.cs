using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IVanityUrlService
{
    Task<List<VanityUrlRecord>> FindAll(CancellationToken cancellationToken);
    Task<List<VanityUrlRecord>> FindService(string service, CancellationToken cancellationToken);
    Task<List<VanityUrlRecord>> FindEnv(string environment, CancellationToken cancellationToken);
    Task<List<VanityUrlRecord>> FindServiceByEnv(string service, string environment, CancellationToken cancellationToken);
}

/**
 * Combines data from NginxVanityUrls, EnabledVanityUrls and Shuttering
 */
public class VanityUrlService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : IVanityUrlService
{
    
    public async Task<List<VanityUrlRecord>> FindAll(CancellationToken cancellationToken)
    {
        var collection = connectionFactory.GetCollection<NginxVanityUrlsRecord>(NginxVanityUrlsService.CollectionName);
        return await collection.Aggregate<VanityUrlRecord>(pipeline).ToListAsync(cancellationToken);
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

    private async Task<List<VanityUrlRecord>> Find(BsonDocument matchStage, CancellationToken cancellationToken)
    {
        var collection = connectionFactory.GetCollection<NginxVanityUrlsRecord>(NginxVanityUrlsService.CollectionName);
        return await collection.Aggregate<VanityUrlRecord>(pipeline.Prepend(matchStage).ToArray()).ToListAsync(cancellationToken);
    }
    
    
    private readonly BsonDocument[] pipeline =
    [
        new BsonDocument("$lookup",
            new BsonDocument
            {
                { "from", ShutteredUrlsService.CollectionName },
                { "localField", "url" },
                { "foreignField", "url" },
                { "as", "shutteredData" }
            }),
        new BsonDocument("$lookup",
            new BsonDocument
            {
                { "from", EnabledVanityUrlsService.CollectionName },
                { "localField", "url" },
                { "foreignField", "url" },
                { "as", "enabledData" }
            }),
        new BsonDocument("$project",
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

public record VanityUrlRecord(string Url, string Environment, string ServiceName, bool Enabled, bool Shuttered)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}