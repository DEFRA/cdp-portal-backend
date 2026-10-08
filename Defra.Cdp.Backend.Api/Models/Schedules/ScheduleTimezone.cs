namespace Defra.Cdp.Backend.Api.Services.Scheduler;

public static class ScheduleTimezone
{
    public static DateTime ToUtc(DateTime dateTime, string timezone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById(timezone));
    }
}