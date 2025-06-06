using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IApiGatewaysService
{
    Task<List<ApiGatewayRecord>> FindService(string service, CancellationToken cancellationToken);
    Task<List<ApiGatewayRecord>> FindServiceByEnv(string service, string environment, CancellationToken cancellationToken);
}

/**
 * Combines data from EnabledApis and Shuttering
 */
public class ApiGatewaysService(IMongoDbClientFactory connectionFactory) : IApiGatewaysService
{
    public async Task<List<ApiGatewayRecord>> FindService(string service, CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument("service", service));
        return await Find(matchStage, cancellationToken);
    }

    public async Task<List<ApiGatewayRecord>> FindServiceByEnv(string service, string environment,
        CancellationToken cancellationToken)
    {
        var matchStage = new BsonDocument("$match",
            new BsonDocument { { "service", service }, { "environment", environment } });
        return await Find(matchStage, cancellationToken);
    }

    private async Task<List<ApiGatewayRecord>> Find(BsonDocument matchStage, CancellationToken cancellationToken)
    {
        var collection = connectionFactory.GetCollection<EnabledApiRecord>(EnabledApisService.CollectionName);
        return await collection.Aggregate<ApiGatewayRecord>(_pipeline.Prepend(matchStage).ToArray(), cancellationToken: cancellationToken).ToListAsync(cancellationToken);
    }


    private readonly BsonDocument[] _pipeline =
    [
        new("$lookup",
            new BsonDocument
            {
                { "from", ShutteredUrlsService.CollectionName },
                { "localField", "api" },
                { "foreignField", "url" },
                { "as", "shutteredData" }
            }),
        new("$project",
            new BsonDocument
            {
                { "api", 1 },
                { "environment", 1 },
                { "service", 1 },
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
                }
            })
    ];
}

[BsonIgnoreExtraElements]
public record ApiGatewayRecord(string Api, string Environment, string Service, bool Shuttered)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}