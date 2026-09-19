using System.Reflection;
using System.Text;
using Laraue.Telegram.NET.Core;
using Laraue.Telegram.NET.Core.Extensions;
using Laraue.Telegram.NET.Core.Routing.Attributes;
using Laraue.Telegram.NET.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Laraue.Telegram.NET.Tests.Telemetry;

[Collection("Integration")]
public class TelegramRouterTelemetryTests
{
    private readonly TestServer _testServer;

    public TelegramRouterTelemetryTests()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services => services
                    .AddTelegramCore(
                        new TelegramNetOptions
                        {
                            Token = "5118223111:ARErD6_712sIDp_OV-UwDDRwemB1IwWW1sE",
                            TelegramUpdatesPoolInterval = 10,
                            TelegramUpdatesInMemoryQueueMaxCount = 10,
                        },
                        [Assembly.GetExecutingAssembly()])
                    .AddInMemoryUpdatesQueue())
                .Configure(b => b.MapTelegramRequests("/test")))
            .Build();

        _testServer = host.GetTestServer();
        host.Start();
    }

    private async Task SendRequestAsync(Update update)
    {
        var client = _testServer.CreateClient();

        await client.PostAsync(
            "test",
            new StringContent(
                JsonSerializer.Serialize(update, JsonBotAPI.Options),
                Encoding.UTF8,
                "text/json"));

        var updatesService = _testServer.Services.GetRequiredService<ITelegramUpdatesService>();
        await updatesService.ProcessQueueUpdatesAsync(batchSize: int.MaxValue);
    }

    [Fact]
    public async Task RouteAsync_ShouldRecordSuccessMetrics_WhenRouteIsMatchedAsync()
    {
        using var metricSpy = new MetricSpy();

        await SendRequestAsync(new Update
        {
            Message = new Message
            {
                From = new User { FirstName = "Ilya", Username = "user", Id = 123, IsBot = false },
                Text = "message",
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 1, Type = ChatType.Private },
            },
        });

        var started = metricSpy.Measurements.Where(m => m.InstrumentName == "telegram.requests.started").ToArray();
        Assert.NotEmpty(started);
        Assert.All(started, s =>
        {
            Assert.Equal(1, s.Value);
            Assert.Contains(s.Tags, t => t is { Key: "telegram.update_type", Value: "Message" });
        });

        var durations = metricSpy.Measurements.Where(m => m.InstrumentName == "telegram.request.duration").ToArray();
        Assert.NotEmpty(durations);
        Assert.All(durations, d =>
        {
            Assert.True(d.Value >= 0);
            Assert.Contains(d.Tags, t => t is { Key: "telegram.status", Value: "success" });
        });

        Assert.DoesNotContain(metricSpy.Measurements, m => m.InstrumentName == "telegram.requests.failed");
    }

    [Fact]
    public async Task RouteAsync_ShouldRecordFailureMetrics_WhenNoRouteIsMatchedAsync()
    {
        using var metricSpy = new MetricSpy();

        await SendRequestAsync(new Update
        {
            Message = new Message
            {
                From = new User { FirstName = "Ilya", Username = "user", Id = 123, IsBot = false },
                Text = "unknownRoute",
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 1, Type = ChatType.Private },
            },
        });

        var failed = metricSpy.Measurements.Where(m => m.InstrumentName == "telegram.requests.failed").ToArray();
        Assert.NotEmpty(failed);
        Assert.All(failed, f =>
        {
            Assert.Equal(1, f.Value);
            Assert.Contains(f.Tags, t => t is { Key: "telegram.status", Value: "not_found" });
        });

        var durations = metricSpy.Measurements.Where(m => m.InstrumentName == "telegram.request.duration").ToArray();
        Assert.NotEmpty(durations);
        Assert.All(durations, d =>
        {
            Assert.True(d.Value >= 0);
            Assert.Contains(d.Tags, t => t is { Key: "telegram.status", Value: "not_found" });
        });
    }

    public class TestTelegramController : Core.Routing.TelegramController
    {
        [TelegramMessageRoute("message")]
        public Task GetMessageAsync() => Task.CompletedTask;
    }
}
