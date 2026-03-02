using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.scheduler;

public interface ISchedulerService
{
    Task Schedule(MongoSchedule schedule, CancellationToken ct);

    Task<List<MongoSchedule>> FetchSchedules(ScheduleMatchers query, CancellationToken ct);

    Task<List<MongoSchedule>> FetchDueSchedules(CancellationToken ct);

    Task<UpdateResult?> UpdateAsync(string? id, UpdateDefinition<MongoSchedule> update, CancellationToken ct);

    Task<bool> DeleteSchedule(string scheduleId, CancellationToken ct);
}

public class SchedulerService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<MongoSchedule>(connectionFactory, CollectionName, loggerFactory),
        ISchedulerService
{
    public const string CollectionName = "schedules";

    protected override List<CreateIndexModel<MongoSchedule>> DefineIndexes(
        IndexKeysDefinitionBuilder<MongoSchedule> builder)
    {
        var indexes = new List<CreateIndexModel<MongoSchedule>>
        {
            new(builder.Ascending(s => s.Enabled).Ascending(s => s.NextRunAt)),
            new(builder.Ascending(s => s.Id), new CreateIndexOptions { Unique = true }),
            new(builder.Ascending(s => s.Task.EntityId)),
        };
        return indexes;
    }

    public async Task Schedule(MongoSchedule mongoSchedule, CancellationToken ct)
    {
        await Collection.InsertOneAsync(mongoSchedule, cancellationToken: ct);
    }

    // todo add pagination
    public async Task<List<MongoSchedule>> FetchSchedules(ScheduleMatchers query, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<MongoSchedule>()
            .Match(query.Filter())
            .Sort(new SortDefinitionBuilder<MongoSchedule>().Descending(d => d.CreatedAt));

        var results = await Collection.AggregateAsync(pipeline, cancellationToken: ct);

        return results
            .ToEnumerable(cancellationToken: ct)
            .Where(r => r != null)
            .OrderByDescending(r => r.CreatedAt)
            .ToList()!;
    }

    public async Task<List<MongoSchedule>> FetchDueSchedules(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var filter = Builders<MongoSchedule>.Filter.And(
            Builders<MongoSchedule>.Filter.Eq(s => s.Enabled, true),
            Builders<MongoSchedule>.Filter.Lte(s => s.NextRunAt, now)
        );

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<UpdateResult?> UpdateAsync(
        string? id,
        UpdateDefinition<MongoSchedule> update,
        CancellationToken ct)
    {
        if (id is null)
        {
            return null;
        }

        return await Collection.UpdateOneAsync(
            Builders<MongoSchedule>.Filter.Eq(s => s.Id, id),
            update,
            cancellationToken: ct);
    }

    public async Task<UpdateResult> UpdateNextRunAtAsync(MongoSchedule schedule, DateTime now, CancellationToken ct)
    {
        return await Collection.UpdateOneAsync(
            Builders<MongoSchedule>.Filter.Eq(s => s.Id, schedule.Id),
            Builders<MongoSchedule>.Update.Set(s => s.NextRunAt, schedule.RecalculateNextRun(now)),
            cancellationToken: ct
        );
    }

    public async Task<bool> DeleteSchedule(string scheduleId, CancellationToken ct = default)
    {
        var result = await Collection.DeleteOneAsync(s => s.Id == scheduleId, ct);
        return result.DeletedCount > 0;
    }
}

public class MongoUserDetails
{
    public string Id { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
}

public record ScheduleMatchers(
    string? Id = null,
    string? EntityId = null,
    DateTime? From = null,
    DateTime? Before = null,
    bool? Enabled = null
)
{
    public FilterDefinition<MongoSchedule> Filter()
    {
        var builder = Builders<MongoSchedule>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(Id))
        {
            filter &= builder.Eq(s => s.Id, Id);
        }

        if (!string.IsNullOrWhiteSpace(EntityId))
        {
            filter &= builder.Eq(s => s.Task.EntityId, EntityId);
        }

        if (From.HasValue)
        {
            filter &= builder.Gte(s => s.Config.StartDate, From.Value);
        }

        if (Before.HasValue)
        {
            filter &= builder.Lte(s => s.Config.EndDate, Before.Value);
        }

        if (Enabled.HasValue)
        {
            filter &= builder.Eq(s => s.Enabled, Enabled.Value);
        }

        return filter;
    }
}