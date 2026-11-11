using Laraue.Telegram.NET.Authentication;
using Laraue.Telegram.NET.Authentication.Middleware;
using Laraue.Telegram.NET.Authentication.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot.Types;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Authentication;

public class ProfileSyncTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly Mock<ITelegramUserQueryService<int>> _queryService = new();
    private readonly TelegramAuthenticationOptions _options = new();
    private readonly TelegramRequestContext<int> _context = new();

    public ProfileSyncTests()
    {
        _queryService.Setup(x => x.FindUserIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramUserId<int>(42));
    }

    [Fact]
    public async Task Profile_ShouldBeUpdatedOncePerInterval()
    {
        var middleware = CreateMiddleware(new InMemoryUserIdByTelegramIdCache<int>(), CreateTracker());

        await RunAsync(middleware, "Ann");
        await RunAsync(middleware, "Anna");
        _time.Now += TimeSpan.FromMinutes(29);
        await RunAsync(middleware, "Anna");
        _time.Now += TimeSpan.FromMinutes(1);
        await RunAsync(middleware, "Anya");

        _queryService.Verify(
            x => x.UpdateAsync(42, It.IsAny<TelegramData>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _queryService.Verify(x => x.UpdateAsync(
            42,
            It.Is<TelegramData>(d => d.Id == 7 && d.FirstName == "Ann" && d.Username == "ann"),
            It.IsAny<CancellationToken>()));
        _queryService.Verify(x => x.UpdateAsync(
            42,
            It.Is<TelegramData>(d => d.FirstName == "Anya"),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Profile_ShouldNotBeUpdated_RightAfterTheUserIsCreated()
    {
        _queryService.Setup(x => x.FindUserIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramUserId<int>?)null);
        _queryService.Setup(x => x.CreateAsync(It.IsAny<TelegramData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        var middleware = CreateMiddleware(new InMemoryUserIdByTelegramIdCache<int>(), CreateTracker());

        await RunAsync(middleware, "Ann");

        _queryService.Verify(x => x.CreateAsync(It.IsAny<TelegramData>(), It.IsAny<CancellationToken>()), Times.Once);
        _queryService.Verify(
            x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<TelegramData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FailedUpdate_ShouldNotFailTheRequest()
    {
        _queryService.Setup(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<TelegramData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database is down"));
        var middleware = CreateMiddleware(new InMemoryUserIdByTelegramIdCache<int>(), CreateTracker());

        var nextCalled = await RunAsync(middleware, "Ann");

        Assert.True(nextCalled);
    }

    [Fact]
    public void Tracker_ShouldNeverSync_WhenTurnedOff()
    {
        _options.ProfileSyncInterval = null;

        Assert.False(CreateTracker().TryStartSync(7));
    }

    [Fact]
    public void Tracker_ShouldLetOnlyOneOfConcurrentCallersSync()
    {
        var tracker = CreateTracker();

        var started = Enumerable.Range(0, 100).AsParallel().Count(_ => tracker.TryStartSync(7));

        Assert.Equal(1, started);
    }

    private InMemoryProfileSyncTracker CreateTracker() => new(Options.Create(_options), _time);

    private AuthTelegramMiddleware<int> CreateMiddleware(
        IUserIdByTelegramIdCache<int> cache,
        IProfileSyncTracker tracker)
    {
        var roleProvider = new Mock<IUserRoleProvider>();
        roleProvider.Setup(x => x.GetUserGroupsAsync(It.IsAny<User>())).ReturnsAsync([]);

        return new AuthTelegramMiddleware<int>(
            new UserService<int>(_queryService.Object),
            _context,
            NullLogger<AuthTelegramMiddleware<int>>.Instance,
            roleProvider.Object,
            new UserSemaphore(),
            cache,
            tracker);
    }

    private async Task<bool> RunAsync(AuthTelegramMiddleware<int> middleware, string firstName)
    {
        _context.Update = new Update
        {
            Message = new Message { From = new User { Id = 7, FirstName = firstName, Username = "ann" } },
        };

        var nextCalled = false;
        await middleware.InvokeAsync(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        return nextCalled;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
