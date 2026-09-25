namespace Laraue.Telegram.NET.Authentication.Services;

public class UserService<TKey> : IUserService<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ITelegramUserQueryService<TKey> _telegramUserQueryService;

    public UserService(
        ITelegramUserQueryService<TKey> telegramUserQueryService)
    {
        _telegramUserQueryService = telegramUserQueryService;
    }
    
    /// <inheritdoc />
    public async Task<LoginResponse<TKey>> LoginOrRegisterAsync(
        TelegramData telegramData,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _telegramUserQueryService.FindUserIdAsync(telegramData.Id, cancellationToken);
        if (existingUser is not null)
            return new LoginResponse<TKey>(existingUser.Id);
        
        var userId = await _telegramUserQueryService.CreateAsync(telegramData, cancellationToken);
        return new LoginResponse<TKey>(userId);
    }
}
