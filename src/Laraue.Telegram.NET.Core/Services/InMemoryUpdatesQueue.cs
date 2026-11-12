using Telegram.Bot.Types;

namespace Laraue.Telegram.NET.Core.Services;

public class InMemoryUpdatesQueue : IUpdatesQueue
{
    private readonly List<Update> _updates = [];
    private readonly Lock _lock = new();
    
    public Task AddAsync(IEnumerable<Update> updates, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _updates.AddRange(updates);
        }
        
        return Task.CompletedTask;
    }

    public Task SetProcessedAsync(Update update, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _updates.Remove(update);
        }
        
        return Task.CompletedTask;
    }

    public Task SetFailedAsync(
        Update update,
        string error,
        string? stackTrace,
        CancellationToken cancellationToken = default)
    {
        return SetProcessedAsync(update, cancellationToken);
    }

    public Task<Update[]> GetAsync(int count, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_updates.Take(count).ToArray());
        }
    }

    public Task<int> GetLastUpdateIdAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_updates.Select(u => u.Id).DefaultIfEmpty().Max());
        }
    }
}