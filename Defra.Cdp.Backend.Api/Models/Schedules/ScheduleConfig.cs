using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using CronExpressionDescriptor;
using Defra.Cdp.Backend.Api.Services.Scheduler;

namespace Defra.Cdp.Backend.Api.Models.Schedules;

[JsonConverter(typeof(ScheduleConfigConverter))]
public abstract class ScheduleConfig: IValidatableObject
{
    public abstract string GetCronExpression();
    public abstract string GetDescription();

    [JsonPropertyName("frequency")] public string Frequency { get; set; } = default!;

    [JsonPropertyName("startDate")] public virtual DateTime StartDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endDate")] public virtual DateTime? EndDate { get; set; }

    [JsonPropertyName("timezone")] public virtual string Timezone { get; set; } = "Europe/London";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(Timezone);
        }
        catch (Exception ex) when (
            ex is TimeZoneNotFoundException ||
            ex is InvalidTimeZoneException)
        {
            return [new ValidationResult(
                $"Invalid IANA timezone: {Timezone}.",
                [nameof(Timezone)])];
        }

        return [];
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class OnceConfig : ScheduleConfig, IValidatableObject
{
    [JsonPropertyName("runAt")] public DateTime RunAt { get; init; }

    [JsonPropertyName("endDate")] public override DateTime? EndDate => RunAt;

    public override string GetCronExpression() =>
        CronBuilder.Daily()
            .AtHour(RunAt.Hour)
            .AtMinute(RunAt.Minute)
            .Build();

    public override string GetDescription() => $"Once at {RunAt:dd MMM yyyy HH:mm} ({Timezone})";

    public new IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = base.Validate(validationContext).ToList();
        
        var runAtUtc = ScheduleTimezone.ToUtc(RunAt, Timezone);

        if (runAtUtc <= DateTime.UtcNow)
        {
            results.Add(new ValidationResult(
                "runAt must be in the future.",
                [nameof(RunAt)]
            ));
        }
        return results;
    }
}

public abstract class RecurringConfig : ScheduleConfig
{
    public override string GetDescription() =>
        $"{ExpressionDescriptor.GetDescription(GetCronExpression())} ({Timezone})";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskTypeEnum
{
    DeployTestSuite,
    DeployService
}