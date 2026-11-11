namespace Laraue.Telegram.NET.Authentication;

/// <summary>
/// Options of the Telegram authentication.
/// </summary>
public sealed class TelegramAuthenticationOptions
{
    /// <summary>
    /// How often the stored Telegram profile of a user is updated from their updates, see
    /// <see cref="Services.ITelegramUserQueryService{TUserKey}.UpdateAsync"/>. Null turns the updates off.
    /// </summary>
    public TimeSpan? ProfileSyncInterval { get; set; } = TimeSpan.FromMinutes(30);
}
