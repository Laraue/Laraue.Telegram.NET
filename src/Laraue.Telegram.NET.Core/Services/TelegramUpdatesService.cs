using Laraue.Core.Threading;
using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Laraue.Telegram.NET.Core.Services;

public interface ITelegramUpdatesService
{
    /// <summary>
    /// Load all new updates to the application <see cref="IUpdatesQueue"/>.
    /// Returns latest update offset.
    /// </summary>
    /// <param name="offset">The first update id to load.</param>
    /// <param name="limit">Max updates to load per request.</param>
    /// <param name="timeout">Seconds Telegram waits for new updates when there are none, 0 for short polling.</param>
    /// <param name="cancellationToken"></param>
    Task<int> LoadNewUpdatesToQueueAsync(
        int offset,
        int limit,
        int timeout = 0,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process all queue updates.
    /// </summary>
    /// <param name="batchSize"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ProcessQueueUpdatesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}

public class TelegramUpdatesService(
    ITelegramBotClient telegramBotClient,
    IUpdatesQueue updatesQueue,
    IServiceProvider serviceProvider,
    ILogger<TelegramUpdatesService> logger,
    IEnumerable<IExceptionHandler> exceptionHandlers)
    : ITelegramUpdatesService
{
    private readonly KeyedSemaphoreSlim<long> _requestByChatIdSemaphore = new (1);
    
    public async Task<int> LoadNewUpdatesToQueueAsync(
        int offset,
        int limit,
        int timeout,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await telegramBotClient
                .GetUpdates(offset, limit, timeout, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (updates.Length > 0)
            {
                await updatesQueue.AddAsync(updates, cancellationToken).ConfigureAwait(false);
            
                offset = updates.Last().Id + 1;
            }
            
            if (updates.Length < limit)
            {
                return offset;
            }
        }

        return offset;
    }

    public async Task ProcessQueueUpdatesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await updatesQueue
                .GetAsync(
                    batchSize,
                    cancellationToken)
                .ConfigureAwait(false);

            if (updates.Length > 0)
            {
                var tasks = updates
                    .Select(update => ProcessUpdateAsync(update, cancellationToken));
        
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            else
            {
                break;
            }
        }
    }
    
    private async Task ProcessUpdateAsync(
        Update update,
        CancellationToken cancellationToken)
    {
        var chatId = update.TryGetChatId();
        chatId ??= 0;
        
        // Different chat messages can be processes in parallel.
        using var requestSemaphore = await _requestByChatIdSemaphore
            .WaitAsync(chatId.Value, cancellationToken)
            .ConfigureAwait(false);
        
        Exception? error = null;

        await using (var routeScope = serviceProvider.CreateAsyncScope())
        {
            var telegramRouter = routeScope.ServiceProvider.GetRequiredService<ITelegramRouter>();

            try
            {
                await telegramRouter
                    .RouteAsync(update, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                error = e;

                foreach (var handler in exceptionHandlers)
                {
                    if (await handler.TryHandleAsync(e, cancellationToken))
                    {
                        error = null;
                        break;
                    }
                }
            }
        }

        // Updates of a batch are processed in parallel, so each one marks itself in the queue through
        // its own scope. The queue may hold a DbContext, which must not be shared between parallel
        // operations, and the router's scope is not reused here, so changes a failed handler left
        // in its DbContext are not saved together with the failed update.
        await using var queueScope = serviceProvider.CreateAsyncScope();
        var queue = queueScope.ServiceProvider.GetRequiredService<IUpdatesQueue>();

        if (error is null)
        {
            await queue
                .SetProcessedAsync(update, cancellationToken)
                .ConfigureAwait(false);

            logger.LogDebug("Processed update {Id}", update.Id);

            return;
        }

        await queue
            .SetFailedAsync(
                update,
                error.Message,
                error.StackTrace,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogError(error, "Error while handling update {Id}, {Message}", update.Id, error.Message);
    }
}