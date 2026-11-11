using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Laraue.Telegram.NET.Authentication.Services;

/// <summary>
/// Keeps the last update time of each user in memory: after a restart every user's profile is updated once
/// on their first update.
/// </summary>
public sealed class InMemoryProfileSyncTracker(
    IOptions<TelegramAuthenticationOptions> options,
    TimeProvider timeProvider)
    : IProfileSyncTracker
{
    private readonly ConcurrentDictionary<long, DateTimeOffset> _syncedAt = new();

    /// <inheritdoc />
    public bool TryStartSync(long telegramId)
    {
        if (options.Value.ProfileSyncInterval is not { } interval)
            return false;

        var now = timeProvider.GetUtcNow();
        // Only one of concurrent callers adds or replaces the time and gets to sync.
        while (true)
        {
            if (_syncedAt.TryGetValue(telegramId, out var syncedAt))
            {
                if (now - syncedAt < interval)
                    return false;
                if (_syncedAt.TryUpdate(telegramId, now, syncedAt))
                    return true;
            }
            else if (_syncedAt.TryAdd(telegramId, now))
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public void MarkSynced(long telegramId)
    {
        _syncedAt[telegramId] = timeProvider.GetUtcNow();
    }
}
