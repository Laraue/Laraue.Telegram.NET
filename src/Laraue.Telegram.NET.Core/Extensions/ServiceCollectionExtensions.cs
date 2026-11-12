using System.Reflection;
using System.Runtime.CompilerServices;
using Laraue.Core.DateTime.Services.Abstractions;
using Laraue.Core.DateTime.Services.Impl;
using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Core.Middleware;
using Laraue.Telegram.NET.Core.Routing;
using Laraue.Telegram.NET.Core.Routing.Attributes;
using Laraue.Telegram.NET.Core.Routing.Middleware;
using Laraue.Telegram.NET.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Laraue.Telegram.NET.Core.Extensions;

/// <summary>
/// Extensions to add Telegram.NET core to the container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="serviceCollection"></param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Adds to the container telegram controllers.
        /// </summary>
        /// <param name="telegramNetOptions"></param>
        /// <param name="controllerAssemblies"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IServiceCollection AddTelegramCore(
            TelegramNetOptions telegramNetOptions,
            Assembly[]? controllerAssemblies = null)
        {
            // Resolved here: the overload below would see this library as its caller.
            return serviceCollection
                .AddSingleton(Options.Create(telegramNetOptions))
                .AddTelegramCore(controllerAssemblies ?? [Assembly.GetCallingAssembly()]);
        }

        /// <summary>
        /// Adds to the container telegram controllers. Require options declaration explicitly.
        /// </summary>
        /// <param name="controllerAssemblies"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IServiceCollection AddTelegramCore(
            Assembly[]? controllerAssemblies = null)
        {
            serviceCollection
                .AddSingleton<IExceptionHandler, RouteNotFoundExceptionHandler>() // as default just log route not found errors
                .AddScoped<MapRequestToTelegramCoreMiddleware>()
                .AddScoped<ITelegramUpdatesService, TelegramUpdatesService>()
                .AddHostedService<TelegramUpdatesLongPoolingBackgroundService>()
                .AddHostedService<TelegramQueueBackgroundService>();

            serviceCollection.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        
            serviceCollection
                .AddSingleton<ITelegramBotClient, TelegramBotClient>(s => 
                    new TelegramBotClient(
                        new TelegramBotClientOptions(
                            s.GetRequiredService<IOptions<TelegramNetOptions>>()
                                .Value
                                .Token)))
                .AddScoped<ITelegramRouter, TelegramRouter>()
                .AddScoped<TelegramRequestContext>();

            serviceCollection.AddTelegramControllers(
                controllerAssemblies
                ?? [Assembly.GetCallingAssembly()]);
            
            serviceCollection.AddOptions<MiddlewareList>();
            serviceCollection.Configure<MiddlewareList>(opt =>
            {
                opt.AddToRoot<ExecuteRouteMiddleware>();
                opt.AddToTop<HandleExceptionsMiddleware>();
            });

            return serviceCollection;
        }

        /// <summary>
        /// Allows to add a custom middleware to the telegram request pipeline.
        /// </summary>
        /// <typeparam name="TMiddleware"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddTelegramMiddleware<TMiddleware>()
            where TMiddleware : class, ITelegramMiddleware
        {
            serviceCollection.Configure<MiddlewareList>(middlewareList =>
            {
                middlewareList.Add<TMiddleware>();
            });

            return serviceCollection;
        }

        private void AddTelegramControllers(IEnumerable<Assembly> controllerAssemblies)
        {
            var controllerTypes = controllerAssemblies
                .SelectMany(x => x.GetTypes())
                .Where(x => x is
                {
                    IsClass: true,
                    IsAbstract: false,
                } && x.IsSubclassOf(typeof(TelegramController)));
        
            foreach (var controllerType in controllerTypes)
            {
                serviceCollection.AddScoped(controllerType);
            
                var methodInfos = controllerType
                    .GetMethods(
                        BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.DeclaredOnly)
                    .Where(x => x
                        .GetCustomAttribute<TelegramBaseRouteAttribute>(true) != null);
            
                foreach (var methodInfo in methodInfos)
                {
                    var routeAttribute = methodInfo
                        .GetCustomAttribute<TelegramBaseRouteAttribute>(true);
                    
                    if (routeAttribute is null)
                    {
                        continue;
                    }
                
                    serviceCollection.AddSingleton<IRoute>(
                        new Route(
                            routeAttribute.TryMatch,
                            methodInfo));
                }
            }
        }
        
        /// <summary>
        /// Store taken telegram requests in memory queue.
        /// It is recommended to use the method only for test purposes and take the real provider from the
        /// 'Laraue.Telegram.NET.UpdatesQueue.EFCore' package for the production due to possible data loss
        /// while application restarting.
        /// </summary>
        /// <returns></returns>
        public IServiceCollection AddInMemoryUpdatesQueue()
        {
            return serviceCollection
                .AddSingleton<IUpdatesQueue, InMemoryUpdatesQueue>();
        }
    }
}