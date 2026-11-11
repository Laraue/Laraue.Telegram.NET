using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Authentication.Services;
using Laraue.Telegram.NET.Core.Extensions;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace Laraue.Telegram.NET.Authentication.Middleware;

public class AuthTelegramMiddleware<TKey> : ITelegramMiddleware
    where TKey : IEquatable<TKey>
{
    private readonly IUserService<TKey> _userService;
    private readonly TelegramRequestContext<TKey> _telegramRequestContext;
    private readonly ILogger<AuthTelegramMiddleware<TKey>> _logger;
    private readonly IUserRoleProvider _userRoleProvider;
    private readonly IUserSemaphore _userSemaphore;
    private readonly IUserIdByTelegramIdCache<TKey> _userIdByTelegramIdCache;
    private readonly IProfileSyncTracker? _profileSyncTracker;

    public AuthTelegramMiddleware(
        IUserService<TKey> userService,
        TelegramRequestContext<TKey> telegramRequestContext,
        ILogger<AuthTelegramMiddleware<TKey>> logger,
        IUserRoleProvider userRoleProvider,
        IUserSemaphore userSemaphore,
        IUserIdByTelegramIdCache<TKey> userIdByTelegramIdCache,
        IProfileSyncTracker? profileSyncTracker = null)
    {
        _userService = userService;
        _telegramRequestContext = telegramRequestContext;
        _logger = logger;
        _userRoleProvider = userRoleProvider;
        _userSemaphore = userSemaphore;
        _userIdByTelegramIdCache = userIdByTelegramIdCache;
        _profileSyncTracker = profileSyncTracker;
    }
    
    /// <inheritdoc />
    public async Task InvokeAsync(Func<CancellationToken, Task> next, CancellationToken ct = default)
    {
        var user = _telegramRequestContext.Update.GetUser();
        if (user is null)
        {
            await next(ct);
            return;
        }
        
        _telegramRequestContext.UserId = await GetOrCreateSystemUserIdAsync(user, ct);
        await SyncProfileAsync(_telegramRequestContext.UserId, user, ct);

        var userGroups = await _userRoleProvider.GetUserGroupsAsync(user);
        _telegramRequestContext.Groups = userGroups.Select(x => x.Name).ToArray();
        
        _logger.LogInformation(
            "Auth as: telegram id: {TelegramId}, system id: {SystemId}, groups: [{Groups}]",
            user.Id,
            _telegramRequestContext.UserId,
            string.Join(',', userGroups.Select(x => x.Name)));
        
        await next(ct);
    }

    private async Task<TKey> GetOrCreateSystemUserIdAsync(User user, CancellationToken cancellationToken)
    {
        using var registrationSemaphore = await _userSemaphore.WaitAsync(user.Id, cancellationToken);

        if (await _userIdByTelegramIdCache.TryGetValueAsync(user.Id, out var systemId))
        {
            return systemId!;
        }
        
        var result = await _userService.LoginOrRegisterAsync(ToTelegramData(user), cancellationToken);
        if (result.IsCreated)
            _profileSyncTracker?.MarkSynced(user.Id);
        
        await _userIdByTelegramIdCache.TryAddAsync(user.Id, result.UserId);
        return result.UserId;
    }

    /// <summary>
    /// Stores the user's current Telegram profile once in <see cref="TelegramAuthenticationOptions.ProfileSyncInterval"/>.
    /// A failure is logged and doesn't fail the update; the next try is after the interval. Without an
    /// <see cref="IProfileSyncTracker"/>, e.g. when the middleware is registered by hand, profiles aren't updated.
    /// </summary>
    private async Task SyncProfileAsync(TKey userId, User user, CancellationToken cancellationToken)
    {
        if (_profileSyncTracker is null || !_profileSyncTracker.TryStartSync(user.Id))
            return;

        try
        {
            await _userService.UpdateProfileAsync(userId, ToTelegramData(user), cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Failed to update the profile of telegram id {TelegramId}", user.Id);
        }
    }

    private static TelegramData ToTelegramData(User user)
    {
        return new TelegramData(user.Id, user.Username, user.LanguageCode, user.FirstName, user.LastName);
    }
}