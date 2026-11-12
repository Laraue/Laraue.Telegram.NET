using System.Net;
using Laraue.Telegram.NET.Core.Services;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Polling;

public class TelegramRequestErrorsTests
{
    public static TheoryData<Exception, bool> Errors => new()
    {
        { new RequestException("Bot API Service Failure", new HttpRequestException("Resource temporarily unavailable")), true },
        { new RequestException("Telegram Bot API error 502: Bad Gateway", HttpStatusCode.BadGateway), true },
        { new ApiRequestException("Too Many Requests", 429), true },
        { new ApiRequestException("Internal Server Error", 500), true },
        { new ApiRequestException("Unauthorized", 401), false },
        { new ApiRequestException("Conflict: terminated by other getUpdates request", 409), false },
        { new InvalidOperationException("Connection already open"), false },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void IsTransient_ShouldSeparateOutagesFromBugs(Exception exception, bool isTransient)
    {
        Assert.Equal(isTransient, TelegramRequestErrors.IsTransient(exception));
    }

    [Fact]
    public void GetRetryAfter_ShouldReadTelegramParameters()
    {
        var exception = new ApiRequestException(
            "Too Many Requests",
            429,
            new ResponseParameters { RetryAfter = 7 });

        Assert.Equal(TimeSpan.FromSeconds(7), TelegramRequestErrors.GetRetryAfter(exception));
        Assert.Null(TelegramRequestErrors.GetRetryAfter(new HttpRequestException()));
    }
}
