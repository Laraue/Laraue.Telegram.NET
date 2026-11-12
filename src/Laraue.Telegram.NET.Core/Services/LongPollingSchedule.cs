namespace Laraue.Telegram.NET.Core.Services;

/// <summary>
/// Decides how long to wait before the next getUpdates request: a short interval while Telegram answers,
/// and a growing one while Telegram fails.
/// </summary>
public sealed class LongPollingSchedule
{
    private static readonly TimeSpan MinRetryInterval = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _interval;
    private readonly TimeSpan _maxRetryInterval;

    private TimeSpan? _retryAfter;

    /// <inheritdoc cref="LongPollingSchedule"/>
    /// <param name="interval">The interval between successful requests.</param>
    /// <param name="maxRetryInterval">The longest interval between retries after failures.</param>
    public LongPollingSchedule(TimeSpan interval, TimeSpan maxRetryInterval)
    {
        _interval = interval;
        _maxRetryInterval = maxRetryInterval;
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
    /// Registers a successful request.
    /// </summary>
    public void OnSuccess()
    {
        ConsecutiveFailures = 0;
        FailingSince = null;
        _retryAfter = null;
    }

    /// <summary>
    /// Registers a failed request.
    /// </summary>
    /// <param name="now">The moment the request failed.</param>
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
    public TimeSpan GetDelay()
    {
        if (ConsecutiveFailures == 0)
        {
            return _interval;
        }

        var baseInterval = _interval > MinRetryInterval ? _interval : MinRetryInterval;
        var exponent = Math.Min(ConsecutiveFailures - 1, 30);
        var backoff = TimeSpan.FromMilliseconds(Math.Min(
            baseInterval.TotalMilliseconds * Math.Pow(2, exponent),
            _maxRetryInterval.TotalMilliseconds));

        return _retryAfter > backoff ? _retryAfter.Value : backoff;
    }
}
