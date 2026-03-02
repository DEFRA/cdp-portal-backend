namespace Defra.Cdp.Backend.Api.Models.Schedules;

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