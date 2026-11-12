using System.Collections.Concurrent;
using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Polling;

/// <summary>
/// Updates of different chats are processed in parallel. A queue backed by a DbContext must not be
/// used by them at the same time, or Npgsql throws "Connection already open" (MIN-46).
/// </summary>
public class ProcessQueueUpdatesTests
{
    [Fact]
    public async Task ParallelUpdates_ShouldNotShareQueueInstance()
    {
        var store = new QueueStore();
        foreach (var id in Enumerable.Range(1, 10))
        {
            store.Pending[id] = new Update
            {
                Id = id,
                Message = new Message { Chat = new Chat { Id = id } },
            };
        }

        await using var provider = new ServiceCollection()
            .AddSingleton(store)
            .AddScoped<IUpdatesQueue, SingleUserQueue>()
            .AddScoped<ITelegramRouter, SlowRouter>()
            .AddSingleton(Mock.Of<ITelegramBotClient>())
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped<ITelegramUpdatesService, TelegramUpdatesService>()
            .BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITelegramUpdatesService>();

        await service.ProcessQueueUpdatesAsync(10, TestContext.Current.CancellationToken);

        Assert.Empty(store.Pending);
        Assert.Empty(store.Failed);
        Assert.Equal(10, store.Processed.Count);
    }

    private sealed class QueueStore
    {
        public ConcurrentDictionary<int, Update> Pending { get; } = new();
        public ConcurrentBag<int> Processed { get; } = [];
        public ConcurrentBag<int> Failed { get; } = [];
    }

    /// <summary>
    /// Behaves like a queue over a DbContext: throws when two operations overlap on one instance.
    /// </summary>
    private sealed class SingleUserQueue(QueueStore store) : IUpdatesQueue
    {
        private int _inFlight;

        public Task AddAsync(IEnumerable<Update> updates, CancellationToken cancellationToken = default)
            => RunAsync(() => { });

        public Task SetProcessedAsync(Update update, CancellationToken cancellationToken = default)
            => RunAsync(() =>
            {
                store.Pending.TryRemove(update.Id, out _);
                store.Processed.Add(update.Id);
            });

        public Task SetFailedAsync(
            Update update,
            string error,
            string? stackTrace,
            CancellationToken cancellationToken = default)
            => RunAsync(() =>
            {
                store.Pending.TryRemove(update.Id, out _);
                store.Failed.Add(update.Id);
            });

        public async Task<Update[]> GetAsync(int count, CancellationToken cancellationToken = default)
        {
            Update[] result = [];
            await RunAsync(() => result = store.Pending.Values.OrderBy(u => u.Id).Take(count).ToArray());

            return result;
        }

        public Task<int> GetLastUpdateIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        private async Task RunAsync(Action action)
        {
            if (Interlocked.Increment(ref _inFlight) > 1)
            {
                Interlocked.Decrement(ref _inFlight);
                throw new InvalidOperationException("Connection already open");
            }

            try
            {
                await Task.Delay(20);
                action();
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class SlowRouter : ITelegramRouter
    {
        public Task RouteAsync(Update update, CancellationToken cancellationToken = default)
            => Task.Delay(10, cancellationToken);
    }
}
