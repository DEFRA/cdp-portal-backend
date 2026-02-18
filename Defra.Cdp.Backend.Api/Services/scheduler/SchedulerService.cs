using System.Text.Json.Serialization;
using Cronos;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.scheduler.TestSuiteDeployment;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.scheduler;

public interface ISchedulerService
{
    Task Schedule(ScheduleRequest schedule, UserDetails userDetails, CancellationToken ct);

    Task<List<Schedule>> FetchSchedules(ScheduleMatchers query, CancellationToken ct);

    Task<List<Schedule>> FetchDueSchedules(CancellationToken ct);

    Task<UpdateResult?> UpdateAsync(string? id, UpdateDefinition<Schedule> update, CancellationToken ct);

    Task<bool> DeleteSchedule(string scheduleId, CancellationToken ct);
}

public class SchedulerService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<Schedule>(connectionFactory, CollectionName, loggerFactory),
        ISchedulerService
{
    public const string CollectionName = "schedules";

    protected override List<CreateIndexModel<Schedule>> DefineIndexes(IndexKeysDefinitionBuilder<Schedule> builder)
    {
        var indexes = new List<CreateIndexModel<Schedule>>
        {
            new(builder.Ascending(s => s.Enabled).Ascending(s => s.NextRunAt)),
            new(builder.Ascending(s => s.Id), new CreateIndexOptions { Unique = true })
        };
        return indexes;
    }

    public async Task Schedule(ScheduleRequest schedule, UserDetails user, CancellationToken ct)
    {
        var mongoTask = ToMongoScheduleTask(schedule);
        var config = ToMongoScheduleConfig(schedule);
        var mongoUser = new MongoUserDetails { Id = user.Id, DisplayName = user.DisplayName };
        var mongoSchedule = new Schedule(
            schedule.TeamId,
            schedule.Enabled,
            schedule.Config.GetCronExpression(),
            schedule.Config.GetDescription(),
            mongoTask,
            config,
            mongoUser
        );
        // mongoSchedule.RecalculateNextRun(schedule.Config.StartDate);

        await Collection.InsertOneAsync(mongoSchedule, cancellationToken: ct);
    }

    private static ScheduleTask ToMongoScheduleTask(ScheduleRequest schedule)
    {
        // Convert the task from the request into the correct polymorphic type
        ScheduleTask mongoTask = schedule.Task switch
        {
            Models.TestSuiteScheduleTask ts => new TestSuiteScheduleTask
            {
                TestSuite = ts.TestSuite,
                Environment = ts.Environment,
                Cpu = ts.Cpu,
                Memory = ts.Memory,
                Profile = ts.Profile
            },
            // add other task types here...
            _ => throw new NotSupportedException($"Unknown task type {schedule.Task.GetType()}")
        };
        return mongoTask;
    }

    private static ScheduleConfig ToMongoScheduleConfig(ScheduleRequest schedule)
    {
        ScheduleConfig config = schedule.Config switch
        {
            Models.OnceConfig c => new OnceConfig { RunAt = c.RunAt },
            Models.DailyRecurringConfig c => new DailyRecurringConfig { Time = c.Time, },
            Models.WeeklyRecurringConfig c => new WeeklyRecurringConfig { Time = c.Time, DaysOfWeek = c.DaysOfWeek, },
            Models.IntervalRecurringConfig c => new IntervalRecurringConfig
            {
                Every = new Interval { Unit = c.Every.Unit.ToString(), Value = c.Every.Value, }
            },
            Models.CronRecurringConfig c => new CronRecurringConfig { Expression = c.Expression }
        };

        config.StartDate = schedule.Config.StartDate;
        config.EndDate = schedule.Config.EndDate;
        config.Frequency = schedule.Config.Frequency;
        return config;
    }

    // todo add pagination
    public async Task<List<Schedule>> FetchSchedules(ScheduleMatchers query, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<Schedule>()
            .Match(query.Filter())
            .Sort(new SortDefinitionBuilder<Schedule>().Descending(d => d.CreatedAt))
            .Group(d => d.TeamId, grp => new { Root = grp.ToList() })
            .Project(grp => grp.Root);

        var results = await Collection.AggregateAsync(pipeline, cancellationToken: ct);

        return results
            .ToEnumerable(cancellationToken: ct)
            .Where(r => r != null)
            .SelectMany(r => r) // flatten arrays
            .OrderByDescending(r => r.CreatedAt)
            .ToList()!;
    }

    public async Task<List<Schedule>> FetchDueSchedules(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var filter = Builders<Schedule>.Filter.And(
            Builders<Schedule>.Filter.Eq(s => s.Enabled, true),
            Builders<Schedule>.Filter.Lte(s => s.NextRunAt, now)
        );

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<UpdateResult?> UpdateAsync(
        string? id,
        UpdateDefinition<Schedule> update,
        CancellationToken ct)
    {
        if (id is null)
        {
            return null;
        }

        return await Collection.UpdateOneAsync(
            Builders<Schedule>.Filter.Eq(s => s.Id, id),
            update,
            cancellationToken: ct);
    }

    public async Task<UpdateResult> UpdateNextRunAtAsync(Schedule schedule, DateTime now, CancellationToken ct)
    {
        return await Collection.UpdateOneAsync(
            Builders<Schedule>.Filter.Eq(s => s.Id, schedule.Id),
            Builders<Schedule>.Update.Set(s => s.NextRunAt, schedule.RecalculateNextRun(now)),
            cancellationToken: ct
        );
    }

    public async Task<bool> DeleteSchedule(string scheduleId, CancellationToken ct = default)
    {
        var result = await Collection.DeleteOneAsync(s => s.Id == scheduleId, ct);
        return result.DeletedCount > 0;
    }
}

public class Schedule
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? MongoId { get; init; } = default!;

    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TeamId { get; init; } = null!;
    public bool Enabled { get; init; }
    public string Cron { get; init; } = null!;
    public string Description { get; init; } = null!;
    public ScheduleTask Task { get; init; } = default!;

    public ScheduleConfig Config { get; init; } = default!;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? NextRunAt { get; set; }

    public MongoUserDetails User { get; init; } = default!;

    public Schedule(string teamId, bool enabled, string cron, string description,
        ScheduleTask task, ScheduleConfig config, MongoUserDetails user)
    {
        TeamId = teamId;
        Enabled = enabled;
        Cron = cron;
        Description = description;
        Task = task;
        User = user;
        Config = config;

        RecalculateNextRun(Config.StartDate);
    }

    public DateTime? RecalculateNextRun(DateTime? from = null)
    {
        var baseTime = from ?? DateTime.UtcNow;

        var next = CronExpression
            .Parse(Cron)
            .GetNextOccurrence(baseTime);

        if (!next.HasValue ||
            (Config.EndDate.HasValue && next.Value > Config.EndDate.Value))
        {
            NextRunAt = null;
            return null;
        }

        NextRunAt = next;
        return NextRunAt;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TestSuiteScheduleTask), nameof(TaskTypeEnum.DeployTestSuite))]
public abstract class ScheduleTask
{
    [BsonRepresentation(BsonType.String)]
    [JsonIgnore]
    public abstract TaskTypeEnum Type { get; protected set; }

    public abstract Task ExecuteAsync(
        IServiceProvider services,
        DateTime? nextRunAt,
        ILogger<object> logger,
        CancellationToken ct);
}

public class TestSuiteScheduleTask : ScheduleTask
{
    [JsonIgnore] public override TaskTypeEnum Type { get; protected set; } = TaskTypeEnum.DeployTestSuite;
    public string TestSuite { get; init; } = default!;
    public string Environment { get; init; } = default!;
    public int Cpu { get; init; } = default!;
    public int Memory { get; init; } = default!;
    public string? Profile { get; init; }

    public override async Task ExecuteAsync(
        IServiceProvider services,
        DateTime? nextRunAt,
        ILogger<object> logger,
        CancellationToken ct)
    {
        var deployer = services.GetRequiredService<ITestSuiteDeployer>();

        var now = DateTime.UtcNow;
        var tolerance = TimeSpan.FromMinutes(5);
        var shouldExecute =
            nextRunAt.HasValue &&
            nextRunAt.Value >= now - tolerance;

        if (shouldExecute)
        {
            await deployer.DeployAsync(
                TestSuite,
                Environment,
                Cpu,
                Memory,
                Profile,
                ct);
        }
        else
        {
            logger.LogWarning(
                "Not executing test-suite {testSuite} to {environment} with next run at {nextRunAt}",
                TestSuite, Environment, nextRunAt);
        }
    }
}

public class MongoUserDetails
{
    public string Id { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "frequency")]
[JsonDerivedType(typeof(OnceConfig), "ONCE")]
[JsonDerivedType(typeof(DailyRecurringConfig), "DAILY")]
[JsonDerivedType(typeof(WeeklyRecurringConfig), "WEEKLY")]
[JsonDerivedType(typeof(IntervalRecurringConfig), "INTERVAL")]
[JsonDerivedType(typeof(CronRecurringConfig), "CRON")]
public abstract class ScheduleConfig
{
    [JsonIgnore]
    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = default!;

    [JsonPropertyName("startDate")] public virtual DateTime StartDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endDate")] public virtual DateTime? EndDate { get; set; }
}

public class OnceConfig : ScheduleConfig
{
    [JsonPropertyName("runAt")] public DateTime RunAt { get; init; }

    [JsonPropertyName("endDate")] public override DateTime? EndDate => RunAt;
}

public class DailyRecurringConfig : ScheduleConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;
}

public class WeeklyRecurringConfig : ScheduleConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;

    [JsonPropertyName("daysOfWeek")] public string[] DaysOfWeek { get; init; } = default!;
}

public class IntervalRecurringConfig : ScheduleConfig
{
    [JsonPropertyName("every")] public Interval Every { get; init; } = default!;
}

public class Interval
{
    [JsonPropertyName("value")] public int Value { get; init; }

    [JsonPropertyName("unit")] public string Unit { get; init; } = default!;
}

public class CronRecurringConfig : ScheduleConfig
{
    [JsonPropertyName("expression")] public string Expression { get; init; } = default!;
}

public record ScheduleMatchers(
    string? Id = null,
    string? TeamId = null,
    DateTime? From = null,
    DateTime? Before = null,
    bool? Enabled = null
)
{
    public FilterDefinition<Schedule> Filter()
    {
        var builder = Builders<Schedule>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(Id))
        {
            filter &= builder.Eq(s => s.Id, Id);
        }

        if (!string.IsNullOrWhiteSpace(TeamId))
        {
            filter &= builder.Eq(s => s.TeamId, TeamId);
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