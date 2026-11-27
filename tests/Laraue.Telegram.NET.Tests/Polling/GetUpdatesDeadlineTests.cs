using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Core;
using Laraue.Telegram.NET.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Polling;

/// <summary>
/// A getUpdates request that hangs is abandoned soon after the long polling timeout, not after
/// HttpClient's 100 seconds (BRD-384).
/// </summary>
public class GetUpdatesDeadlineTests
{
    [Fact]
    public async Task HungRequest_ShouldFailAsTransientTimeout()
    {
        var service = CreateService(HangingClient());

        var exception = await Assert.ThrowsAsync<RequestException>(
            () => service.LoadNewUpdatesToQueueAsync(0, 10, 0, TestContext.Current.CancellationToken));

        Assert.Equal("Request timed out", exception.Message);
        Assert.True(TelegramRequestErrors.IsTransient(exception));
    }

    [Fact]
    public async Task Stopping_ShouldCancelInsteadOfTimingOut()
    {
        var service = CreateService(HangingClient(), graceMilliseconds: 60_000);
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LoadNewUpdatesToQueueAsync(0, 10, 0, stopping.Token));
    }

    private static TelegramUpdatesService CreateService(ITelegramBotClient client, int graceMilliseconds = 50)
    {
        return new TelegramUpdatesService(
            client,
            Mock.Of<IUpdatesQueue>(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<TelegramUpdatesService>.Instance,
            [],
            Options.Create(new TelegramNetOptions
            {
                Token = "test",
                LongPoolingTimeoutGrace = graceMilliseconds,
            }));
    }

    /// <summary>
    /// A client whose getUpdates never answers, like a request over a dead connection.
    /// </summary>
    private static ITelegramBotClient HangingClient()
    {
        var client = new Mock<ITelegramBotClient>();
        client
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Update[]>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IRequest<Update[]> _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return [];
            });

        return client.Object;
    }
}
