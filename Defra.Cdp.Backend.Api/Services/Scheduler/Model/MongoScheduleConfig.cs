using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Scheduler.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "frequency")]
[JsonDerivedType(typeof(MongoOnceConfig), "ONCE")]
[JsonDerivedType(typeof(MongoDailyRecurringConfig), "DAILY")]
[JsonDerivedType(typeof(MongoWeeklyRecurringConfig), "WEEKLY")]
[JsonDerivedType(typeof(MongoIntervalRecurringConfig), "INTERVAL")]
[JsonDerivedType(typeof(MongoCronRecurringConfig), "CRON")]
public abstract class MongoScheduleConfig
{
    [JsonIgnore]
    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = default!;

    [JsonPropertyName("startDate")] public virtual DateTime StartDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endDate")] public virtual DateTime? EndDate { get; set; }
}

public class MongoOnceConfig : MongoScheduleConfig
{
    [JsonPropertyName("runAt")] public DateTime RunAt { get; init; }

    [JsonPropertyName("endDate")] public override DateTime? EndDate => RunAt;
}

public class MongoDailyRecurringConfig : MongoScheduleConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;
}

public class MongoWeeklyRecurringConfig : MongoScheduleConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;

    [JsonPropertyName("daysOfWeek")] public string[] DaysOfWeek { get; init; } = default!;
}

public class MongoIntervalRecurringConfig : MongoScheduleConfig
{
    [JsonPropertyName("every")] public MongoInterval Every { get; init; } = default!;
}

public class MongoInterval
{
    [JsonPropertyName("value")] public int Value { get; init; }

    [JsonPropertyName("unit")] public string Unit { get; init; } = default!;
}

public class MongoCronRecurringConfig : MongoScheduleConfig
{
    [JsonPropertyName("expression")] public string Expression { get; init; } = default!;
}