using System.Globalization;
using System.Reflection;
using System.Text;
using Laraue.Telegram.NET.Abstractions;
using Laraue.Telegram.NET.Abstractions.Request;
using Laraue.Telegram.NET.Authentication.Attributes;
using Laraue.Telegram.NET.Authentication.Middleware;
using Laraue.Telegram.NET.Authentication.Protectors;
using Laraue.Telegram.NET.Authentication.Services;
using Laraue.Telegram.NET.Core;
using Laraue.Telegram.NET.Core.Extensions;
using Laraue.Telegram.NET.Core.Routing;
using Laraue.Telegram.NET.Core.Routing.Attributes;
using Laraue.Telegram.NET.Core.Services;
using Laraue.Telegram.NET.Interceptors.Extensions;
using Laraue.Telegram.NET.Interceptors.Services;
using Laraue.Telegram.NET.Localization;
using Laraue.Telegram.NET.Localization.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Laraue.Telegram.NET.Tests.Controllers;

[Collection("Integration")]
public class ControllerTests
{
    private readonly TestServer _testServer;
    private readonly Mock<ITestChecker> _testChecker = new();
    
    public ControllerTests()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services
                        .AddTelegramCore(
                            new TelegramNetOptions
                            {
                                Token = "5118223111:ARErD6_712sIDp_OV-UwDDRwemB1IwWW1sE",
                                TelegramUpdatesPoolInterval = 10,
                                TelegramUpdatesInMemoryQueueMaxCount = 10,
                            },
                            [Assembly.GetExecutingAssembly()])
                        .AddInMemoryUpdatesQueue()
                        .AddScoped<TelegramRequestContext<string>>()
                        .AddScoped<TelegramRequestContext>(
                            sp => sp.GetRequiredService<TelegramRequestContext<string>>())
                        .AddTelegramRequestLocalization()
                        .Configure<TelegramRequestLocalizationOptions>(opt =>
                        {
                            opt.DefaultLanguage = "en";
                            opt.AvailableLanguages = ["en", "ru"];
                        })
                        .AddTelegramRequestInterceptors<InMemoryInterceptorState, string>(
                            [Assembly.GetExecutingAssembly()],
                            ServiceLifetime.Singleton)
                        .AddTelegramMiddleware<AuthTelegramMiddleware<string>>()
                        .AddSingleton<IUserSemaphore, UserSemaphore>()
                        .AddSingleton<IUserIdByTelegramIdCache<string>, InMemoryUserIdByTelegramIdCache<string>>()
                        .AddScoped<IUserService<string>, MockedUserService>()
                        .AddScoped<IUserRoleProvider>(_ => new StaticUserRoleProvider(
                            Options.Create(new RoleUsers
                            {
                                ["Protected.View"] = ["JohnLennon"]
                            })))
                        .AddScoped<IControllerProtector, UserShouldBeInGroupProtector<string>>()
                        .AddSingleton(_testChecker.Object);

                    // Each test processes the queue itself (SendRequestAsync). The background processor would race
                    // with it and could still be handling an update when the test checks the result.
                    services.Remove(services.Single(d => d.ImplementationType == typeof(TelegramQueueBackgroundService)));
                })
                .Configure(b => b.MapTelegramRequests("/test")))
            .Build();

        _testServer = host.GetTestServer();
        host.Start();
    }

    private async Task<string> SendRequestAsync(Update update)
    {
        var client = _testServer.CreateClient();
        
        var resp = await client
            .PostAsync(
                "test",
                new StringContent(JsonSerializer.Serialize(update, JsonBotAPI.Options),
                    Encoding.UTF8,
                    "text/json"));

        var updatesService = _testServer.Services.GetRequiredService<ITelegramUpdatesService>();
        await updatesService.ProcessQueueUpdatesAsync(batchSize: int.MaxValue);

        resp.EnsureSuccessStatusCode();

        return await resp.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task MessageRoute_ShouldResponseCorrectlyAsync()
    {
        await SendRequestAsync(new Update
        {
            Message = new Message
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = "user",
                    Id = 123,
                    IsBot = false,
                },
                Text = "message",
                Date = DateTime.UtcNow,
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Private
                }
            },
        });
        
        _testChecker.Verify(x => x.Call("get: message"));
    }
    
    [Fact]
    public async Task CallbackRoute_ShouldParsePathParametersCorrectlyAsync()
    {
        await SendRequestAsync(new Update
        {
            CallbackQuery = new CallbackQuery
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = "user",
                    Id = 123,
                    IsBot = false,
                },
                Data = new CallbackRoutePath("callback/12")
                    .WithQueryParameter("name", "Alex"),
                Id = "123",
                ChatInstance = "123",
            },
        });
        
        _testChecker.Verify(x => x.Call("AlexAlex1212Alex"));
    }
    
    [Fact]
    public async Task CallbackRoute_ShouldBeRoutedCorrectly_WhenMethodIsPostAsync()
    {
        await SendRequestAsync(new Update
        {
            CallbackQuery = new CallbackQuery
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = "user",
                    Id = 123,
                    IsBot = false,
                },
                Data = new CallbackRoutePath("callback/12", RouteMethod.Post),
                Id = "123",
                ChatInstance = "123",
            },
        });

        _testChecker.Verify(x => x.Call("post: 12"));
    }
    
    [Fact]
    public async Task MessageRoute_ShouldWorkWithInterceptorsAsync()
    {
        var sendMessage = new Update
        {
            Message = new Message
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = "user",
                    Id = 123,
                    IsBot = false,
                },
                Text = "awaitResponse",
                Date = DateTime.UtcNow,
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Private
                }
            },
        };
        
        var result = await SendRequestAsync(sendMessage);
        _testChecker.Verify(x => x.Call("awaitResponse"));
        
        await SendRequestAsync(sendMessage);
        _testChecker.Verify(x => x.Call("intercepted"));
        
        result = await SendRequestAsync(sendMessage);
        _testChecker.Verify(x => x.Call("awaitResponse"));
    }
    
    [Theory]
    [InlineData("JohnLennon", true)]
    [InlineData("AlexFerrari", false)]
    public async Task MessageRoute_ShouldBeProtectedAsync(string userName, bool shouldRouteBeAvailable)
    {
        var result = await SendRequestAsync(new Update
        {
            Message = new Message
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = userName,
                    Id = 123,
                    IsBot = false,
                },
                Text = "protected",
                Date = DateTime.UtcNow,
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Private
                }
            },
        });

        if (shouldRouteBeAvailable)
        {
            _testChecker.Verify(x => x.Call("protected"));
        }
        else
        {
            _testChecker.VerifyNoOtherCalls();
        }
    }
    
    [Fact]
    public async Task MessageRoute_ShouldBeLocalizedAsync()
    {
        await SendRequestAsync(new Update
        {
            Message = new Message
            {
                From = new User
                {
                    FirstName = "Ilya",
                    Username = "user",
                    Id = 123,
                    IsBot = false,
                    LanguageCode = "ru"
                },
                Text = "localizedRoute",
                Date = DateTime.UtcNow,
                Chat = new Chat
                {
                    Id = 1,
                    Type = ChatType.Private
                },
            },
        });
        
        _testChecker.Verify(x => x.Call("Locale: ru ru"));
    }

    public class TestTelegramController : TelegramController
    {
        private readonly IInterceptorState<string> _responseAwaiter;
        private readonly ITestChecker _testChecker;

        public TestTelegramController(IInterceptorState<string> responseAwaiter, ITestChecker testChecker)
        {
            _responseAwaiter = responseAwaiter;
            _testChecker = testChecker;
        }

        [TelegramMessageRoute("message")]
        public Task GetMessageAsync(TelegramRequestContext<string> requestContext)
        {
            _testChecker.Call($"get: {requestContext.Update.Message!.Text}");
            
            return Task.CompletedTask;
        }
        
        [TelegramCallbackRoute("callback/{id}")]
        public Task GetCallbackAsync(
            [FromPath] int id,
            [FromPath("id")] int alsoId,
            [FromQuery] string? name,
            [FromQuery("name")] string? alsoName,
            [FromQuery] QueryParameters queryParameters)
        {
            _testChecker.Call($"{name}{alsoName}{id}{alsoId}{queryParameters.Name}");
            
            return Task.CompletedTask;
        }
        
        [TelegramCallbackRoute("callback/{id}", RouteMethod.Post)]
        public Task ExecuteCallbackAsync([FromPath] int id)
        {
            _testChecker.Call($"post: {id}");
            
            return Task.CompletedTask;
        }
        
        [TelegramMessageRoute("awaitResponse")]
        public async Task ExecuteMessageWithResponseAwaiterAsync(TelegramRequestContext<string> requestContext)
        {
            await _responseAwaiter.SetAsync<MessageResponseAwaiter, MessageResponseAwaiterParameters>(
                requestContext.UserId!,
                new MessageResponseAwaiterParameters("Hi"));
            
            _testChecker.Call(requestContext.Update.Message!.Text);
        }
        
        [RequiresUserRole("Protected.View")]
        [TelegramMessageRoute("protected")]
        public Task ExecuteProtectedAsync(TelegramRequestContext<string> requestContext)
        {
            _testChecker.Call(requestContext.Update.Message!.Text);
            
            return Task.CompletedTask;
        }
        
        [TelegramMessageRoute("localizedRoute")]
        public void ExecuteLocalizedRoute(TelegramRequestContext<string> requestContext)
        {
            _testChecker.Call($"Locale: {CultureInfo.CurrentCulture} {CultureInfo.CurrentUICulture}");
        }
    }

    public sealed class QueryParameters
    {
        public string Name { get; init; }
    }

    private sealed class MessageResponseAwaiter : BaseRequestInterceptor<string, MessageResponseAwaiterModel, MessageResponseAwaiterParameters>
    {
        private readonly ITestChecker _testChecker;
        
        public override string Id => "MessageResponseAwaiter";

        protected override Task ValidateAsync(
            TelegramRequestContext<string> requestContext,
            InterceptResult<MessageResponseAwaiterModel> interceptResult,
            MessageResponseAwaiterParameters? interceptorContext,
            CancellationToken cancellationToken = default)
        {
            interceptResult.SetResult(new MessageResponseAwaiterModel("awaited"));

            return Task.CompletedTask;
        }

        protected override Task<ExecutionState> ExecuteRouteAsync(
            TelegramRequestContext<string> requestContext,
            MessageResponseAwaiterModel model,
            MessageResponseAwaiterParameters? interceptorContext,
            CancellationToken cancellationToken = default)
        {
            _testChecker.Call("intercepted");
            
            return Task.FromResult(ExecutionState.FullyExecuted);
        }

        public MessageResponseAwaiter(TelegramRequestContext<string> requestContext, IInterceptorState<string> interceptorState, ITestChecker testChecker)
            : base(requestContext, interceptorState)
        {
            _testChecker = testChecker;
        }
    }
    private sealed record MessageResponseAwaiterParameters(string Message);
    private sealed record MessageResponseAwaiterModel(string Message);

    private class InMemoryInterceptorState : BaseInterceptorState<string>
    {
        private readonly Dictionary<string, (string, object)> _interceptors = new ();

        public InMemoryInterceptorState(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public override Task<string?> GetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_interceptors.TryGetValue(userId, out var value)
                ? value.Item1
                : default);
        }

        protected override Task<TContext?> GetInterceptorContextInternalAsync<TContext>(
            string userId,
            CancellationToken cancellationToken = default)
            where TContext : class
        {
            _interceptors.TryGetValue(userId, out var value);

            var e = value.Item2 as TContext;

            return (Task.FromResult(e));
        }

        protected override Task SetInterceptorAsync<TContext>(
            string userId,
            string id,
            TContext context,
            CancellationToken cancellationToken = default)
        {
            _interceptors[userId] = (id, context);
            
            return Task.CompletedTask;
        }

        public override Task ResetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            _interceptors.Remove(userId);
            
            return Task.CompletedTask;
        }
    }

    public sealed class MockedUserService : IUserService<string>
    {
        public Task<LoginResponse<string>> LoginOrRegisterAsync(
            TelegramData loginData,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LoginResponse<string>("123"));
        }
    }
    
    public interface ITestChecker
    {
        void Call(string? testValue);
    }
}