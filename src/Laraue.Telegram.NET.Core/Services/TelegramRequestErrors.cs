using System.Net;
using Telegram.Bot.Exceptions;

namespace Laraue.Telegram.NET.Core.Services;

/// <summary>
/// Tells errors of Telegram requests that go away by themselves from the ones that need a fix.
/// </summary>
public static class TelegramRequestErrors
{
    /// <summary>
    /// Whether the request may succeed when retried later: Telegram is unreachable,
    /// answers with a server error or asks to slow down.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        return exception switch
        {
            ApiRequestException apiException => IsTransient(apiException.ErrorCode),
            RequestException { HttpStatusCode: { } statusCode } => IsTransient((int)statusCode),
            RequestException { InnerException: HttpRequestException or TaskCanceledException or IOException } => true,
            HttpRequestException => true,
            _ => false,
        };
    }

    /// <summary>
    /// How long Telegram asked to wait before the next request, if it did.
    /// </summary>
    public static TimeSpan? GetRetryAfter(Exception exception)
    {
        return exception is ApiRequestException { Parameters.RetryAfter: { } seconds }
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static bool IsTransient(int statusCode)
    {
        return statusCode == (int)HttpStatusCode.TooManyRequests || statusCode >= 500;
    }
}
