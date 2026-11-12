using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laraue.Telegram.NET.Core.Services;

/// <summary>
/// Service thar receives updates from the tg and run their processing.
/// </summary>
public class TelegramUpdatesLongPoolingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<TelegramNetOptions> _options;
    private readonly ILogger<TelegramUpdatesLongPoolingBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// How long Telegram should be unreachable before the outage is logged as an error, not a warning.
    /// </summary>
    private static readonly TimeSpan OutageErrorAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// While Telegram is unreachable a warning is logged for the first failure and then for every n-th one.
    /// </summary>
    private const int WarningEveryFailures = 10;

    /// <inheritdoc cref="TelegramUpdatesLongPoolingBackgroundService"/>.
    public TelegramUpdatesLongPoolingBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TelegramNetOptions> options,
        ILogger<TelegramUpdatesLongPoolingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Long pooling is not required.
        if (!string.IsNullOrWhiteSpace(_options.Value.WebhookUrl))
        {
            _logger.LogInformation("Telegram webhook url configured, long pooling is disabled");
            
            return;
        }
        
        _logger.LogInformation("Telegram webhook url is not configured, long pooling is enabled");
        
        var batchSize = _options.Value.LongPoolingBatchSize
            ?? throw new InvalidOperationException($"{nameof(TelegramNetOptions.LongPoolingBatchSize)} setup is required");

        var longPoolingInterval = _options.Value.LongPoolingInterval
            ?? throw new InvalidOperationException($"{nameof(TelegramNetOptions.LongPoolingInterval)} setup is required");
        
        var options = _options.Value;
        var schedule = new LongPollingSchedule(
            TimeSpan.FromMilliseconds(longPoolingInterval),
            options.LongPoolingIdleInterval is { } idleInterval ? TimeSpan.FromMilliseconds(idleInterval) : null,
            TimeSpan.FromMilliseconds(options.LongPoolingIdleAfter),
            TimeSpan.FromMilliseconds(options.LongPoolingMaxRetryInterval),
            _timeProvider.GetUtcNow());

        var currentOffset = await LoadLastOffsetAsync(stoppingToken);
        var isIdle = false;
        var isOutageErrorLogged = false;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var telegramUpdatesService = scope.ServiceProvider.GetRequiredService<ITelegramUpdatesService>();

                var newOffset = await telegramUpdatesService
                    .LoadNewUpdatesToQueueAsync(currentOffset, batchSize, options.LongPoolingTimeout, stoppingToken)
                    .ConfigureAwait(false);

                var now = _timeProvider.GetUtcNow();
                if (schedule.FailingSince is { } failingSince)
                {
                    _logger.LogInformation(
                        "Telegram is reachable again after {Failures} failed requests in {Outage}",
                        schedule.ConsecutiveFailures,
                        now - failingSince);
                }

                schedule.OnSuccess(newOffset != currentOffset, now);
                isOutageErrorLogged = false;
                currentOffset = newOffset;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                var now = _timeProvider.GetUtcNow();
                schedule.OnFailure(now, TelegramRequestErrors.GetRetryAfter(e));
                isOutageErrorLogged |= LogFailure(e, schedule, now, isOutageErrorLogged);
            }

            var delayFrom = _timeProvider.GetUtcNow();
            var delay = schedule.GetDelay(delayFrom);

            if (schedule.ConsecutiveFailures == 0 && schedule.IsIdle(delayFrom) != isIdle)
            {
                isIdle = !isIdle;
                _logger.LogInformation(
                    isIdle
                        ? "No telegram updates for {IdleAfter}, polling every {Delay}"
                        : "Got telegram updates after {IdleAfter} or more of silence, polling every {Delay}",
                    TimeSpan.FromMilliseconds(options.LongPoolingIdleAfter),
                    delay);
            }

            await Task
                .Delay(delay, _timeProvider, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Logs a failed request so that an outage gives a few lines instead of one per retry.
    /// Returns whether the error about a long outage was logged.
    /// </summary>
    private bool LogFailure(
        Exception exception,
        LongPollingSchedule schedule,
        DateTimeOffset now,
        bool isOutageErrorLogged)
    {
        var delay = schedule.GetDelay(now);

        if (!TelegramRequestErrors.IsTransient(exception))
        {
            _logger.LogError(exception, "Error while requesting updates from telegram, retrying in {Delay}", delay);

            return false;
        }

        var failures = schedule.ConsecutiveFailures;
        var outage = now - schedule.FailingSince!.Value;

        if (!isOutageErrorLogged && outage >= OutageErrorAfter)
        {
            _logger.LogError(
                exception,
                "Telegram has been unreachable for {Outage}, {Failures} failed requests, retrying in {Delay}",
                outage,
                failures,
                delay);

            return true;
        }

        if (failures == 1 || failures % WarningEveryFailures == 0)
        {
            _logger.LogWarning(
                "Telegram is unreachable, {Failures} failed requests in {Outage}, retrying in {Delay}: {Reason}",
                failures,
                outage,
                delay,
                exception.Message);
        }

        return false;
    }

    private async Task<int> LoadLastOffsetAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var updatesQueue = scope.ServiceProvider.GetRequiredService<IUpdatesQueue>();

        return await updatesQueue.GetLastUpdateIdAsync(cancellationToken);
    }
}