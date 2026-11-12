using Laraue.Core.DateTime.Services.Abstractions;
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
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// How long Telegram should be unreachable before the outage is logged as an error, not a warning.
    /// </summary>
    private static readonly TimeSpan OutageErrorAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// While Telegram is unreachable a warning is logged for the first failure and then for every n-th one.
    /// </summary>
    private const int WarningEveryFailures = 10;

    private const int DefaultTimeoutSeconds = 30;
    private static readonly TimeSpan DefaultMaxRetryInterval = TimeSpan.FromMinutes(1);

    /// <inheritdoc cref="TelegramUpdatesLongPoolingBackgroundService"/>.
    public TelegramUpdatesLongPoolingBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TelegramNetOptions> options,
        ILogger<TelegramUpdatesLongPoolingBackgroundService> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
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
        var timeout = options.LongPoolingTimeout ?? DefaultTimeoutSeconds;
        var schedule = new LongPollingSchedule(
            TimeSpan.FromMilliseconds(longPoolingInterval),
            ToTimeSpan(options.LongPoolingMaxRetryInterval) ?? DefaultMaxRetryInterval);

        var currentOffset = await LoadLastOffsetAsync(stoppingToken);
        var isOutageErrorLogged = false;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var telegramUpdatesService = scope.ServiceProvider.GetRequiredService<ITelegramUpdatesService>();

                var newOffset = await telegramUpdatesService
                    .LoadNewUpdatesToQueueAsync(currentOffset, batchSize, timeout, stoppingToken)
                    .ConfigureAwait(false);

                var now = _dateTimeProvider.UtcNow;
                if (schedule.FailingSince is { } failingSince)
                {
                    _logger.LogInformation(
                        "Telegram is reachable again after {Failures} failed requests in {Outage}",
                        schedule.ConsecutiveFailures,
                        now - failingSince);
                }

                schedule.OnSuccess();
                isOutageErrorLogged = false;
                currentOffset = newOffset;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                var now = _dateTimeProvider.UtcNow;
                schedule.OnFailure(now, TelegramRequestErrors.GetRetryAfter(e));
                isOutageErrorLogged |= LogFailure(e, schedule, now, isOutageErrorLogged);
            }

            await Task
                .Delay(schedule.GetDelay(), stoppingToken)
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
        DateTime now,
        bool isOutageErrorLogged)
    {
        var delay = schedule.GetDelay();

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

    private static TimeSpan? ToTimeSpan(int? milliseconds)
    {
        return milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null;
    }

    private async Task<int> LoadLastOffsetAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var updatesQueue = scope.ServiceProvider.GetRequiredService<IUpdatesQueue>();

        return await updatesQueue.GetLastUpdateIdAsync(cancellationToken);
    }
}