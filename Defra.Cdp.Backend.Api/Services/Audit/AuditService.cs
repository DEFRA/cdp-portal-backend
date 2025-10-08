using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Audit;

public interface IAuditService
{
    Task Audit(AuditDto auditDto, CancellationToken cancellationToken);

    Task<List<AuditDto>> FindAll(CancellationToken cancellationToken);
}

public class AuditService(
    IMongoDbClientFactory connectionFactory,
    ICloudWatchMetricsService cloudWatchMetricsService,
    ILoggerFactory loggerFactory)
    : MongoService<Audit>(connectionFactory, CollectionName, loggerFactory),
        IAuditService
{
    public const string CollectionName = "audit";
    

    protected override List<CreateIndexModel<Audit>> DefineIndexes(IndexKeysDefinitionBuilder<Audit> builder)
    {
        var indexKeys = builder.Descending(a => a.PerformedAt).Ascending(a => a.Category);
        var indexModel = new CreateIndexModel<Audit>(indexKeys);
        return [indexModel];
    }

    public async Task Audit(AuditDto auditDto, CancellationToken cancellationToken)
    {
        await cloudWatchMetricsService.IncrementAsync(
            metricName: $"{auditDto.Category}Alerts",
            dimensions: new Dictionary<string, string>
            {
                ["Action"] = auditDto.Action
            },
            ct: cancellationToken);

        var detailsDoc = auditDto.Details.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new BsonDocument()
            : BsonDocument.Parse(auditDto.Details.GetRawText());

        var mongoAudit = new Audit(
            auditDto.Category,
            auditDto.Action,
            auditDto.PerformedBy,
            auditDto.PerformedAt,
            detailsDoc
        );

        await Collection.InsertOneAsync(mongoAudit, cancellationToken: cancellationToken);
    }

    public async Task<List<AuditDto>> FindAll(CancellationToken cancellationToken)
    {
        var items = await Collection
            .Find(Builders<Audit>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return items.Select(a => new AuditDto(
            a.Category,
            a.Action,
            a.PerformedBy,
            a.PerformedAt,
            ToJsonElement(a.Details))).ToList();
    }
    private static JsonElement ToJsonElement(BsonDocument doc)
    {
        var json = doc.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
        {
            OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson
        });
        using var jd = JsonDocument.Parse(json);
        return jd.RootElement.Clone(); // important: clone before disposing
    }
}


public record AuditDto(string Category, string Action, UserDetails PerformedBy, DateTime PerformedAt, JsonElement Details);

public record Audit(string Category, string Action, UserDetails PerformedBy, DateTime PerformedAt, BsonDocument Details)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}