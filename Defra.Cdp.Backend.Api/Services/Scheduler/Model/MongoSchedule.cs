using System.Text.Json.Serialization;
using Cronos;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Defra.Cdp.Backend.Api.Services.scheduler.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Services.Scheduler.Model;

public class MongoSchedule
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? MongoId { get; init; } = default!;

    public string Id { get; init; } = Guid.NewGuid().ToString();
    public bool Enabled { get; init; }
    public string Cron { get; init; } = null!;
    public string Description { get; init; } = null!;
    public MongoScheduleTask Task { get; init; } = default!;

    public MongoScheduleConfig Config { get; init; } = default!;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? NextRunAt { get; set; }

    public MongoUserDetails User { get; init; } = default!;

    public MongoSchedule(bool enabled, string cron, string description,
        MongoScheduleTask task, MongoScheduleConfig config, MongoUserDetails user)
    {
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