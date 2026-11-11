namespace Laraue.Telegram.NET.Authentication.Services;

/// <summary>
/// Remembers when the Telegram profile of each user was last stored, to update it only once in a while.
/// </summary>
public interface IProfileSyncTracker
{
    /// <summary>
    /// Whether the profile of the user is due for an update. When it is, the update is counted as done now, so
    /// concurrent updates of the same user don't all start one.
    /// </summary>
    bool TryStartSync(long telegramId);

    /// <summary>
    /// The profile of the user was just stored, e.g. when the user was created.
    /// </summary>
    void MarkSynced(long telegramId);
}
