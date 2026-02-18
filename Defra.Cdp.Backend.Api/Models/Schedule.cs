using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using CronExpressionDescriptor;

namespace Defra.Cdp.Backend.Api.Models;

public class ScheduleRequest
{
    [JsonPropertyName("teamId")] public string TeamId { get; init; } = default!;

    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;

    [JsonPropertyName("task")] public ScheduleTask Task { get; init; } = default!;

    [JsonPropertyName("config")] public ScheduleConfig Config { get; init; } = default!;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskTypeEnum
{
    DeployTestSuite
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TestSuiteScheduleTask), "DeployTestSuite")]
public abstract class ScheduleTask
{
    [JsonPropertyName("type")] public TaskTypeEnum Type { get; protected set; }
}

public class TestSuiteScheduleTask : ScheduleTask
{
    [JsonPropertyName("testSuite")] public string TestSuite { get; init; } = default!;

    [JsonPropertyName("environment")] public string Environment { get; init; } = default!;

    [JsonPropertyName("cpu")] public int Cpu { get; init; }

    [JsonPropertyName("memory")] public int Memory { get; init; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}

[JsonConverter(typeof(ScheduleConfigConverter))]
public abstract class ScheduleConfig
{
    public abstract string GetCronExpression();
    public abstract string GetDescription();

    [JsonPropertyName("frequency")] public string Frequency { get; set; } = default!;

    [JsonPropertyName("startDate")] public virtual DateTime StartDate { get; protected set; } = DateTime.UtcNow;

    [JsonPropertyName("endDate")] public virtual DateTime? EndDate { get; protected set; }
}

public class OnceConfig : ScheduleConfig, IValidatableObject
{
    [JsonPropertyName("runAt")] public DateTime RunAt { get; init; }

    [JsonPropertyName("endDate")] public override DateTime? EndDate => RunAt;

    public override string GetCronExpression() =>
        CronBuilder.Daily()
            .AtHour(RunAt.Hour)
            .AtMinute(RunAt.Minute)
            .Build();

    public override string GetDescription() => $"Once at {RunAt:dd MMM yyyy HH:mm}";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var now = DateTime.UtcNow;

        if (RunAt < now)
            yield return new ValidationResult(
                "runAt must be in the future (UTC).",
                [nameof(RunAt)]
            );
    }
}

public abstract class RecurringConfig : ScheduleConfig
{
    public override string GetDescription() =>
        ExpressionDescriptor.GetDescription(GetCronExpression());
}

public class DailyRecurringConfig : RecurringConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;

    public override string GetCronExpression()
    {
        var ts = TimeSpan.ParseExact(Time, "hh\\:mm", null);

        return CronBuilder.Daily()
            .AtHour(ts.Hours)
            .AtMinute(ts.Minutes)
            .Build();
    }
}

public class WeeklyRecurringConfig : RecurringConfig
{
    [JsonPropertyName("time")] public string Time { get; init; } = default!;

    [JsonPropertyName("daysOfWeek")] public string[] DaysOfWeek { get; init; } = default!;

    public override string GetCronExpression()
    {
        var ts = TimeSpan.ParseExact(Time, "hh\\:mm", null);

        return CronBuilder.Daily()
            .AtHour(ts.Hours)
            .AtMinute(ts.Minutes)
            .OnDayOfWeek(DaysOfWeek.Select(ParseDay).ToArray())
            .Build();
    }

    private static DayOfWeek ParseDay(string day) =>
        Enum.Parse<DayOfWeek>(day, ignoreCase: true);
}

public class IntervalRecurringConfig : RecurringConfig
{
    [JsonPropertyName("every")] public Interval Every { get; init; } = default!;

    public override string GetCronExpression()
    {
        return Every.Unit switch
        {
            IntervalUnit.Minutes => $"*/{Every.Value} * * * *",
            IntervalUnit.Hours => $"0 */{Every.Value} * * *",
            IntervalUnit.Days => $"0 0 */{Every.Value} * *",
            _ => throw new NotSupportedException()
        };
    }
}

public class Interval
{
    [JsonPropertyName("value")] public int Value { get; init; }

    [JsonPropertyName("unit")] public IntervalUnit Unit { get; init; } = default!;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntervalUnit
{
    Minutes,
    Hours,
    Days
}

public class CronRecurringConfig : RecurringConfig
{
    [JsonPropertyName("expression")] public string Expression { get; init; } = default!;

    public override string GetCronExpression() => Expression;
}

public class ScheduleConfigConverter : JsonConverter<ScheduleConfig>
{
    public override ScheduleConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("frequency", out var freq)) throw new JsonException("frequency missing from config");
        return freq.GetString() switch
        {
            "ONCE" => JsonSerializer.Deserialize<OnceConfig>(root.GetRawText(), options),
            "DAILY" => JsonSerializer.Deserialize<DailyRecurringConfig>(root.GetRawText(), options),
            "WEEKLY" => JsonSerializer.Deserialize<WeeklyRecurringConfig>(root.GetRawText(), options),
            "INTERVAL" => JsonSerializer.Deserialize<IntervalRecurringConfig>(root.GetRawText(), options),
            "CRON" => JsonSerializer.Deserialize<CronRecurringConfig>(root.GetRawText(), options),
            _ => throw new JsonException("Unknown schedule config type.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ScheduleConfig value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

public class CronBuilder
{
    private string _minute = "0";
    private string _hour = "0";
    private string _dayOfMonth = "*";
    private string _month = "*";
    private string _dayOfWeek = "*";

    private CronBuilder()
    {
    }

    public static CronBuilder Daily() => new();

    public CronBuilder AtHour(int h)
    {
        if (h is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(h));
        _hour = h.ToString();
        return this;
    }

    public CronBuilder AtMinute(int m)
    {
        if (m is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(m));
        _minute = m.ToString();
        return this;
    }

    public CronBuilder OnDayOfWeek(params DayOfWeek[] days)
    {
        if (days == null || days.Length == 0) throw new ArgumentException("At least one day must be provided");
        _dayOfWeek = string.Join(",", days.Select(d => ((int)d + 0) % 7));
        return this;
    }

    public CronBuilder EveryNHours(int n)
    {
        if (n is <= 0 or > 23) throw new ArgumentOutOfRangeException(nameof(n));
        _hour = $"*/{n}";
        return this;
    }

    public CronBuilder EveryNMinutes(int n)
    {
        if (n is <= 0 or > 59) throw new ArgumentOutOfRangeException(nameof(n));
        _minute = $"*/{n}";
        return this;
    }

    public CronBuilder EveryNDays(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        _dayOfMonth = $"*/{n}";
        return this;
    }

    public string Build() => $"{_minute} {_hour} {_dayOfMonth} {_month} {_dayOfWeek}";
}