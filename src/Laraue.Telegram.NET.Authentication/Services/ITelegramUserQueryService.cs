namespace Laraue.Telegram.NET.Authentication.Services;

/// <summary>
/// Maps a Telegram account to the system user it belongs to. Implemented by the consuming
/// application - the library itself never needs the system user's shape, only its identifier.
/// </summary>
/// <typeparam name="TUserKey">Type of the system user identifier.</typeparam>
public interface ITelegramUserQueryService<TUserKey>
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// Finds the system user linked to the passed telegram id. Returns null if there is none yet.
    /// </summary>
    Task<TelegramUserId<TUserKey>?> FindUserIdAsync(
        long telegramId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new system user for the Telegram account described by <paramref name="telegramData"/>.
    /// What (if anything) to store from the Telegram profile is up to the implementation.
    /// </summary>
    /// <returns>Identifier of the created user.</returns>
    Task<TUserKey> CreateAsync(
        TelegramData telegramData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifier of a system user found by <see cref="ITelegramUserQueryService{TUserKey}.FindUserIdAsync"/>.
/// A wrapper rather than a bare <typeparamref name="TUserKey"/>, so "not found" can be expressed as
/// null for value-type keys (e.g. <see cref="Guid"/>) too.
/// </summary>
public sealed record TelegramUserId<TUserKey>(TUserKey Id)
    where TUserKey : IEquatable<TUserKey>;
