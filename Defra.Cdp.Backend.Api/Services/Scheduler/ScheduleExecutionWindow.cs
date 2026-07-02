namespace Defra.Cdp.Backend.Api.Services.Scheduler;

public static class ScheduleExecutionWindow
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public static bool IsDue(DateTime? nextRunAt, DateTime? now = null)
    {
        now ??= DateTime.UtcNow;
        return nextRunAt.HasValue && nextRunAt.Value >= now.Value - Tolerance;
    }
}
