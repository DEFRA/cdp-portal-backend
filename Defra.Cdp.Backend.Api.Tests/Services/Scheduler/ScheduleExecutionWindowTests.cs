using Defra.Cdp.Backend.Api.Services.Scheduler;

namespace Defra.Cdp.Backend.Api.Tests.Services.Scheduler;

public class ScheduleExecutionWindowTests
{
    private static readonly DateTime Now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsDue_returns_false_when_nextRunAt_is_null()
    {
        Assert.False(ScheduleExecutionWindow.IsDue(null, Now));
    }

    [Theory]
    [InlineData(0)]           // exactly now
    [InlineData(1)]           // in the future
    [InlineData(-1)]          // 1 minute ago
    [InlineData(-5)]          // exactly at tolerance boundary
    public void IsDue_returns_true_when_nextRunAt_is_within_tolerance(int minutesFromNow)
    {
        var nextRunAt = Now.AddMinutes(minutesFromNow);

        Assert.True(ScheduleExecutionWindow.IsDue(nextRunAt, Now));
    }

    [Fact]
    public void IsDue_returns_false_when_nextRunAt_is_before_tolerance_window()
    {
        var nextRunAt = Now.AddMinutes(-5).AddSeconds(-1);

        Assert.False(ScheduleExecutionWindow.IsDue(nextRunAt, Now));
    }
}
