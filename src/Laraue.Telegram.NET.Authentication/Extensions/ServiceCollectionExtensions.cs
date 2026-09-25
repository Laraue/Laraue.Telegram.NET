using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Authentication.Middleware;
using Laraue.Telegram.NET.Authentication.Protectors;
using Laraue.Telegram.NET.Authentication.Services;
using Laraue.Telegram.NET.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Telegram.NET.Authentication.Extensions;

/// <summary>
/// Extensions to add Authentication functionality to the container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="serviceCollection"></param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Add authentication middleware with <see cref="TelegramRequestContext{TKey}"/>
        /// to the container and configure identity.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TTelegramUserQueryService"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddTelegramAuthentication<TKey, TTelegramUserQueryService>()
            where TKey : IEquatable<TKey>
            where TTelegramUserQueryService : class, ITelegramUserQueryService<TKey> 
        {
            return serviceCollection.AddTelegramAuthentication<TKey, TTelegramUserQueryService, TelegramRequestContext<TKey>>();
        }

        /// <summary>
        /// Add authentication middleware and the passed <see cref="TTelegramRequestContext"/>
        /// to the container and configure identity.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TTelegramUserQueryService"></typeparam>
        /// <typeparam name="TTelegramRequestContext"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddTelegramAuthentication<TKey, TTelegramUserQueryService, TTelegramRequestContext>()
            where TKey : IEquatable<TKey>
            where TTelegramRequestContext : TelegramRequestContext<TKey>
            where TTelegramUserQueryService : class, ITelegramUserQueryService<TKey>
        {
            serviceCollection.AddTelegramMiddleware<AuthTelegramMiddleware<TKey>>();
        
            serviceCollection.AddScoped<TTelegramRequestContext>();
            serviceCollection.AddSingleton<IUserSemaphore, UserSemaphore>();
            serviceCollection.AddSingleton<IUserIdByTelegramIdCache<TKey>, InMemoryUserIdByTelegramIdCache<TKey>>();

            if (typeof(TTelegramRequestContext) != typeof(TelegramRequestContext<TKey>))
            {
                serviceCollection.AddScoped<TelegramRequestContext<TKey>>(
                    sp => sp.GetRequiredService<TTelegramRequestContext>());
            }
        
            serviceCollection.AddScoped<TelegramRequestContext>(
                sp => sp.GetRequiredService<TTelegramRequestContext>());
        
            serviceCollection.AddScoped<ITelegramUserQueryService<TKey>, TTelegramUserQueryService>();
            serviceCollection.AddScoped<IUserService<TKey>, UserService<TKey>>();

            serviceCollection.UseUserRolesProvider<DefaultUserRoleProvider>();
            return serviceCollection.AddScoped<IControllerProtector, UserShouldBeInGroupProtector<TKey>>();
        }

        /// <summary>
        /// Start use role provider for the user.
        /// </summary>
        public IServiceCollection UseUserRolesProvider<TUserGroupProvider>()
            where TUserGroupProvider : class, IUserRoleProvider
        {
            return serviceCollection.AddScoped<IUserRoleProvider, TUserGroupProvider>();
        }
    }
}