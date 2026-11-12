using Laraue.Telegram.NET.Core.Services;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Polling;

public class LongPollingScheduleTests
{
    private static readonly DateTime Start = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(1);

    [Fact]
    public void Delay_ShouldBeInterval_WhenRequestsSucceed()
    {
        var schedule = CreateSchedule();

        schedule.OnSuccess();

        Assert.Equal(Interval, schedule.GetDelay());
    }

    [Fact]
    public void Delay_ShouldGrowUpToMaxRetryInterval_WhileRequestsFail()
    {
        var schedule = CreateSchedule();
        var delays = new List<TimeSpan>();

        for (var i = 0; i < 9; i++)
        {
            schedule.OnFailure(Start);
            delays.Add(schedule.GetDelay());
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

        Assert.Equal(TimeSpan.FromSeconds(42), schedule.GetDelay());
    }

    [Fact]
    public void Success_ShouldResetFailures()
    {
        var schedule = CreateSchedule();
        schedule.OnFailure(Start);
        schedule.OnFailure(Start.AddSeconds(1));
        Assert.Equal(2, schedule.ConsecutiveFailures);
        Assert.Equal(Start, schedule.FailingSince);

        schedule.OnSuccess();

        Assert.Equal(0, schedule.ConsecutiveFailures);
        Assert.Null(schedule.FailingSince);
        Assert.Equal(Interval, schedule.GetDelay());
    }

    private static LongPollingSchedule CreateSchedule()
    {
        return new LongPollingSchedule(Interval, MaxRetry);
    }
}
