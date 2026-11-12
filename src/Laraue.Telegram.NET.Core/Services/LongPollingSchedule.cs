namespace Laraue.Telegram.NET.Core.Services;

/// <summary>
/// Decides how long to wait before the next getUpdates request: a short interval while the bot gets updates,
/// a longer one after it got none for a while, and a growing one while Telegram fails.
/// </summary>
public sealed class LongPollingSchedule
{
    private static readonly TimeSpan MinRetryInterval = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _activeInterval;
    private readonly TimeSpan? _idleInterval;
    private readonly TimeSpan _idleAfter;
    private readonly TimeSpan _maxRetryInterval;

    private DateTime _lastUpdatesAt;
    private TimeSpan? _retryAfter;

    /// <inheritdoc cref="LongPollingSchedule"/>
    /// <param name="activeInterval">The interval while the bot gets updates.</param>
    /// <param name="idleInterval">The interval after <paramref name="idleAfter"/> without updates, null to never change it.</param>
    /// <param name="idleAfter">How long without updates makes the bot idle.</param>
    /// <param name="maxRetryInterval">The longest interval between retries after failures.</param>
    /// <param name="startedAt">The moment polling started, counted as the last activity.</param>
    public LongPollingSchedule(
        TimeSpan activeInterval,
        TimeSpan? idleInterval,
        TimeSpan idleAfter,
        TimeSpan maxRetryInterval,
        DateTime startedAt)
    {
        _activeInterval = activeInterval;
        _idleInterval = idleInterval;
        _idleAfter = idleAfter;
        _maxRetryInterval = maxRetryInterval;
        _lastUpdatesAt = startedAt;
    }

    /// <summary>
    /// Failed requests in a row since the last successful one.
    /// </summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>
    /// When the current series of failures started, null when the last request succeeded.
    /// </summary>
    public DateTime? FailingSince { get; private set; }

    /// <summary>
    /// Whether the bot got no updates for long enough to poll with the idle interval.
    /// </summary>
    public bool IsIdle(DateTime now)
    {
        return _idleInterval is not null && now - _lastUpdatesAt >= _idleAfter;
    }

    /// <summary>
    /// Registers a successful request.
    /// </summary>
    public void OnSuccess(bool hasUpdates, DateTime now)
    {
        if (hasUpdates)
        {
            _lastUpdatesAt = now;
        }

        ConsecutiveFailures = 0;
        FailingSince = null;
        _retryAfter = null;
    }

    /// <summary>
    /// Registers a failed request.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="retryAfter">How long Telegram asked to wait before the next request, if it did.</param>
    public void OnFailure(DateTime now, TimeSpan? retryAfter = null)
    {
        ConsecutiveFailures++;
        FailingSince ??= now;
        _retryAfter = retryAfter;
    }

    /// <summary>
    /// How long to wait before the next request.
    /// </summary>
    public TimeSpan GetDelay(DateTime now)
    {
        if (ConsecutiveFailures > 0)
        {
            var baseInterval = _activeInterval > MinRetryInterval ? _activeInterval : MinRetryInterval;
            var exponent = Math.Min(ConsecutiveFailures - 1, 30);
            var backoff = TimeSpan.FromMilliseconds(Math.Min(
                baseInterval.TotalMilliseconds * Math.Pow(2, exponent),
                _maxRetryInterval.TotalMilliseconds));

            return _retryAfter > backoff ? _retryAfter.Value : backoff;
        }

        return IsIdle(now) ? _idleInterval!.Value : _activeInterval;
    }
}
