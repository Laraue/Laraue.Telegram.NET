using Laraue.Telegram.NET.Core;
using Laraue.Telegram.NET.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laraue.Telegram.NET.Tests.Controllers;

public class AddTelegramCoreTests
{
    [Fact]
    public void AddTelegramCore_ShouldRegisterCallerControllers_WhenOptionsPassedWithoutAssemblies()
    {
        var services = new ServiceCollection()
            .AddTelegramCore(new TelegramNetOptions { Token = "1:test" });

        Assert.Contains(services, d => d.ServiceType == typeof(ControllerTests.TestTelegramController));
    }

    [Fact]
    public void AddTelegramCore_ShouldRegisterCallerControllers_WhenNothingPassed()
    {
        var services = new ServiceCollection()
            .AddTelegramCore();

        Assert.Contains(services, d => d.ServiceType == typeof(ControllerTests.TestTelegramController));
    }
}
