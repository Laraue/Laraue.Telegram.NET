using System.Diagnostics;
using System.Text.Json;
using Laraue.Core.Exceptions.Web;
using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Core.Extensions;
using Laraue.Telegram.NET.Core.Routing.Middleware;
using Laraue.Telegram.NET.Core.Telemetry;
using Laraue.Telegram.NET.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace Laraue.Telegram.NET.Core.Routing;

/// <inheritdoc />
public sealed class TelegramRouter : ITelegramRouter
{
    private readonly MiddlewareList _middlewareList;
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramRequestContext _telegramRequestContext;
    private readonly ILogger<TelegramRouter> _logger;
    
    /// <summary>
    /// Initializes a new instance of <see cref="TelegramRouter"/>.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="telegramRequestContext"></param>
    /// <param name="middlewareList"></param>
    /// <param name="logger"></param>
    public TelegramRouter(
        IServiceProvider serviceProvider,
        TelegramRequestContext telegramRequestContext,
        IOptions<MiddlewareList> middlewareList,
        ILogger<TelegramRouter> logger)
    {
        _middlewareList = middlewareList.Value;
        _serviceProvider = serviceProvider;
        _telegramRequestContext = telegramRequestContext;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task RouteAsync(Update update, CancellationToken cancellationToken = default)
    {
        var sw = new Stopwatch();
        sw.Start();

        _telegramRequestContext.Update = update;
        using var requestScope = RequestScope.Start(update);

        var middlewares = new LinkedList<ITelegramMiddleware>();
        foreach (var middlewareType in _middlewareList.Items)
        {
            middlewares.AddFirst(
                (ITelegramMiddleware) ActivatorUtilities.CreateInstance(_serviceProvider, middlewareType));
        }

        Func<CancellationToken, Task> invokeDelegate = _ => Task.CompletedTask;
        
        var middlewareNode = middlewares.Last;
        while (middlewareNode != null)
        {
            var middlewareRef = middlewareNode.Value;
            var delegateRef = invokeDelegate.Invoke;
            
            invokeDelegate = async (ct) =>
            {
                var middlewareSw = new Stopwatch();
                middlewareSw.Start();

                var middlewareName = middlewareRef.GetType();
                _logger.LogDebug("Middleware {Name} is executing", middlewareName);
                
                await middlewareRef.InvokeAsync(delegateRef, ct).ConfigureAwait(false);
                _logger.LogDebug(
                    "Middleware {Name} executed for {Time} ms",
                    middlewareName,
                    middlewareSw.ElapsedMilliseconds);
            };
            
            middlewareNode = middlewareNode.Previous;
        }

        try
        {
            await invokeDelegate(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            requestScope.Complete(ex);
            throw;
        }

        var executedRoute = _telegramRequestContext.GetExecutedRoute();

        if (executedRoute is not null)
        {
            _logger.LogInformation(
                "Request time {Time} ms, route: {RouteName} executed",
                sw.ElapsedMilliseconds,
                executedRoute);

            requestScope.Complete(executedRoute.Details);
        }
        else
        {
            var exception = new RouteNotFoundException(
                sw.ElapsedMilliseconds,
                update);

            requestScope.Complete(exception);

            throw exception;
        }
    }
}