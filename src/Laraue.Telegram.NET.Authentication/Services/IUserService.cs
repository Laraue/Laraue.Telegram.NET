namespace Laraue.Telegram.NET.Authentication.Services;

/// <summary>
/// Contains methods to register or login telegram user.
/// </summary>
public interface IUserService<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Should return system id for the telegram user.
    /// </summary>
    Task<LoginResponse<TKey>> LoginOrRegisterAsync(
        TelegramData loginData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the stored Telegram profile of an existing user. Does nothing by default, so implementations
    /// written before it keep working.
    /// </summary>
    Task UpdateProfileAsync(
        TKey userId,
        TelegramData telegramData,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Login response with the system user id.
/// </summary>
public record LoginResponse<TKey>(TKey UserId) where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Whether the user was created by this login, so their profile is fresh.
    /// </summary>
    public bool IsCreated { get; init; }
}

/// <summary>
/// Telegram credentials to get system user id.
/// </summary>
public record TelegramData(
    long Id,
    string? Username,
    string? LanguageCode,
    string? FirstName,
    string? LastName);