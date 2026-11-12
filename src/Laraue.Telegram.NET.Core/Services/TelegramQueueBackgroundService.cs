using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laraue.Telegram.NET.Core.Services;

public class TelegramQueueBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<TelegramNetOptions> _options;
    private readonly ILogger<TelegramQueueBackgroundService> _logger;

    public TelegramQueueBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TelegramNetOptions> options,
        ILogger<TelegramQueueBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batchSize = _options.Value.TelegramUpdatesInMemoryQueueMaxCount;
        var waitInterval = _options.Value.TelegramUpdatesPoolInterval;

        if (batchSize <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(TelegramNetOptions.TelegramUpdatesInMemoryQueueMaxCount)} setup is required");
        }
        
        if (waitInterval <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(TelegramNetOptions.TelegramUpdatesPoolInterval)} setup is required");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var updatesService = scope.ServiceProvider.GetRequiredService<ITelegramUpdatesService>();

                await updatesService.ProcessQueueUpdatesAsync(batchSize, stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                // An unhandled exception would stop the whole host, keep processing the queue instead.
                _logger.LogError(e, "Error while processing the telegram updates queue");
            }
            
            await Task
                .Delay(waitInterval, stoppingToken)
                .ConfigureAwait(false);
        }
    }
}