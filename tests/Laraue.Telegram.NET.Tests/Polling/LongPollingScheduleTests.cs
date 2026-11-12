using Laraue.Telegram.NET.Core.Services;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Polling;

public class LongPollingScheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Active = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(1);

    [Fact]
    public void Delay_ShouldBeActiveInterval_WhenUpdatesCameRecently()
    {
        var schedule = CreateSchedule();

        schedule.OnSuccess(hasUpdates: true, Start.AddMinutes(5));

        Assert.Equal(Active, schedule.GetDelay(Start.AddMinutes(14)));
        Assert.False(schedule.IsIdle(Start.AddMinutes(14)));
    }

    [Fact]
    public void Delay_ShouldBeIdleInterval_WhenNoUpdatesForIdlePeriod()
    {
        var schedule = CreateSchedule();

        schedule.OnSuccess(hasUpdates: false, Start.AddMinutes(9));

        Assert.Equal(Idle, schedule.GetDelay(Start.AddMinutes(10)));
        Assert.True(schedule.IsIdle(Start.AddMinutes(10)));
    }

    [Fact]
    public void Delay_ShouldReturnToActiveInterval_WhenUpdateCameWhileIdle()
    {
        var schedule = CreateSchedule();
        Assert.Equal(Idle, schedule.GetDelay(Start.AddMinutes(30)));

        schedule.OnSuccess(hasUpdates: true, Start.AddMinutes(30));

        Assert.Equal(Active, schedule.GetDelay(Start.AddMinutes(30)));
    }

    [Fact]
    public void Delay_ShouldStayActive_WhenIdleIntervalIsNotConfigured()
    {
        var schedule = new LongPollingSchedule(Active, null, IdleAfter, MaxRetry, Start);

        Assert.Equal(Active, schedule.GetDelay(Start.AddHours(5)));
        Assert.False(schedule.IsIdle(Start.AddHours(5)));
    }

    [Fact]
    public void Delay_ShouldGrowUpToMaxRetryInterval_WhileRequestsFail()
    {
        var schedule = CreateSchedule();
        var delays = new List<TimeSpan>();

        for (var i = 0; i < 9; i++)
        {
            schedule.OnFailure(Start);
            delays.Add(schedule.GetDelay(Start));
        }

        Assert.Equal(
            new[] { 1, 2, 4, 8, 16, 32, 60, 60, 60 }.Select(s => TimeSpan.FromSeconds(s)),
            delays);
    }

    [Fact]
    public void Delay_ShouldRespectRetryAfter_WhenTelegramAskedToWaitLonger()
    {
        var schedule = CreateSchedule();

        schedule.OnFailure(Start, TimeSpan.FromSeconds(42));

        Assert.Equal(TimeSpan.FromSeconds(42), schedule.GetDelay(Start));
    }

    [Fact]
    public void Success_ShouldResetFailures()
    {
        var schedule = CreateSchedule();
        schedule.OnFailure(Start);
        schedule.OnFailure(Start.AddSeconds(1));
        Assert.Equal(2, schedule.ConsecutiveFailures);
        Assert.Equal(Start, schedule.FailingSince);

        schedule.OnSuccess(hasUpdates: false, Start.AddSeconds(3));

        Assert.Equal(0, schedule.ConsecutiveFailures);
        Assert.Null(schedule.FailingSince);
        Assert.Equal(Active, schedule.GetDelay(Start.AddSeconds(3)));
    }

    private static LongPollingSchedule CreateSchedule()
    {
        return new LongPollingSchedule(Active, Idle, IdleAfter, MaxRetry, Start);
    }
}
