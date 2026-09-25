# Laraue.Telegram.NET

This library contains infrastructure code to write testable telegram bots for one to one conversations.
The library use https://github.com/TelegramBots/Telegram.Bot package inside to communicate with Telegram.

## Laraue.Telegram.NET.Core

[![latest version](https://img.shields.io/nuget/v/Laraue.Telegram.NET.Core)](https://www.nuget.org/packages/Laraue.Telegram.NET.Core)
[![latest version](https://img.shields.io/nuget/dt/Laraue.Telegram.NET.Core)](https://www.nuget.org/packages/Laraue.Telegram.NET.Core)

The basic idea of the library is to register all possible telegram routes in classes
inherited from _TelegramController_.

```csharp
public class MenuController : TelegramController
{
    private readonly IMenuService _service;

    public SettingsController(IMenuService service)
    {
        _service = service;
    }
    
    [TelegramMessageRoute("/start")] // When the user will send the '/start' message
    public Task ShowMenuAsync(TelegramRequestContext requestContext)
    {
        return _service.HandleStartAsync(requestContext.Update.Message!); // execute this code
    }
```

To process callback queries can be used attribute _TelegramCallbackRouteAttribute_,
any request type can be handled by implementing the attribute inherited from _TelegramBaseRouteAttribute_.

To start using this and other controllers, the library should be added with the following way:

A. Using webhooks
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TelegramNetOptions>();
builder.Services.Configure<TelegramNetOptions>(builder.Configuration.GetSection("Telegram"));

builder
    .Services
    .AddTelegramCore(new TelegramBotClientOptions("5118263652:AAHiPDQ8kVcbs2WZWG4Z..."))
    .AddInMemoryUpdatesQueue();

var app = builder.Build();
app.MapTelegramRequests("/api/telegram");
app.Run();
```

B. Using long pooling
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TelegramNetOptions>();
builder.Services.Configure<TelegramNetOptions>(builder.Configuration.GetSection("Telegram"));

builder
    .Services
    .AddTelegramCore(new TelegramBotClientOptions("5118263652:AAHiPDQ8kVcbs2WZWG4Z..."))
    .AddInMemoryUpdatesQueue();

var app = builder.Build();
app.Run();
```

The library takes the decision what way to use based on the property `WebhookUrl` in the app settings.
When the setting is set, the application considers Webhook mode is used, otherwise long pooling mode
is active.

The token in the TelegramBotClientOptions can be taken via @BotFather bot in the Telegram.
_MapTelegramRequests_ method setup the address which will listen callbacks from the telegram.
This address should be set also on the telegram side by calling the next url:

**Note**: The AddInMemoryUpdatesQueue call adds to the container InMemory queue to store telegram updates.
It is not recommended due to possible updates loss. The better way is to use 
`AddEfCoreUpdatesQueue<MyDbContext>` from the package `Laraue.Telegram.NET.UpdatesQueue.EFCore`
to store all updates queue in the database or implementing custom `IUpdatesQueue`.
```
https://api.telegram.org/bot5118263652:AAHiPDQ8kVcbs2WZWG4Z.../setWebhook?url=https://your.host/api/telegram
```

### Metrics and tracing

`Laraue.Telegram.NET.Core` reports a trace and a set of metrics for every Telegram update processed
through `TelegramRouter`, on the `"Laraue.Telegram.NET"` `ActivitySource`/`Meter`:

| Instrument                    | Type      | Description                                                        |
|--------------------------------|-----------|----------------------------------------------------------------------|
| `telegram.requests.started`   | Counter   | Number of updates that started processing.                          |
| `telegram.requests.failed`    | Counter   | Number of updates that failed (no route matched, or a handler threw). |
| `telegram.request.duration`   | Histogram | Duration of processing an update, in milliseconds.                  |

Measurements are tagged with `telegram.update_type` (e.g. `Message`, `CallbackQuery`),
`telegram.route_name` (when a route was matched) and `telegram.status` (`success`, `not_found` or `error`).

No OpenTelemetry package reference is required to emit this telemetry - it uses only
`System.Diagnostics`/`System.Diagnostics.Metrics`. To actually collect it, add
`Laraue.Telegram.NET.OpenTelemetry` and call the one-liner:

```bash
dotnet add package Laraue.Telegram.NET.OpenTelemetry
```

```csharp
builder.Services
    .AddLaraueTelegramTelemetry(
        configureTracing: tracing => tracing.AddOtlpExporter(),
        configureMetrics: metrics => metrics.AddOtlpExporter());
```

Or, if the app already builds its own `TracerProviderBuilder`/`MeterProviderBuilder`, subscribe manually:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddLaraueTelegram())
    .WithMetrics(m => m.AddLaraueTelegram());
```

## Laraue.Telegram.NET.Authentication

[![latest version](https://img.shields.io/nuget/v/Laraue.Telegram.NET.Authentication)](https://www.nuget.org/packages/Laraue.Telegram.NET.Authentication)
[![latest version](https://img.shields.io/nuget/dt/Laraue.Telegram.NET.Authentication)](https://www.nuget.org/packages/Laraue.Telegram.NET.Authentication)

As soon as each request is usually associated with the specific user, it is convenient to
have information about system user in the request instead of manual finding
user by telegram id when it is required.
The library resolves the system user id for every incoming update and puts it into the request
context. It doesn't need to know the shape of your user model - only how to find a user's id by
their telegram id, and how to create one. Implement ```ITelegramUserQueryService<TKey>```
(example for users with an id of _Guid_ type):
```csharp
public class TelegramUserQueryService(MyDbContext context) : ITelegramUserQueryService<Guid>
{
    public Task<TelegramUserId<Guid>?> FindUserIdAsync(long telegramId, CancellationToken ct = default)
    {
        return context.Users
            .Where(u => u.TelegramId == telegramId)
            .Select(u => new TelegramUserId<Guid>(u.Id))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(TelegramData telegramData, CancellationToken ct = default)
    {
        // Store whatever you need from the telegram profile (username, names, language code).
        var user = new User { TelegramId = telegramData.Id, UserName = telegramData.Username };
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);

        return user.Id;
    }
}
```
Register Auth functionality in the container.
```csharp
services.AddTelegramCore(new TelegramBotClientOptions(builder.Configuration["Telegram:Token"]!))
    .AddTelegramAuthentication<Guid, TelegramUserQueryService>()
```

After that in each telegram controller method can be retrieved ```TelegramRequestContext<Guid>``` which contains information
about the user made the request.

```csharp
public class UserController : TelegramController
{
    [TelegramMessageRoute("/me")]
    public void PrintMyId(TelegramRequestContext<Guid> requestContext)
    {
        Console.WriteLine(requestContext.UserId); // user id in our storage
        Console.WriteLine(requestContext.Update.GetUserId()); // telegram user id from the update
    }
```

To prevent writing generic type for each request, new context class can be created and added to the container.
```csharp
public sealed class RequestContext : TelegramRequestContext<Guid>
{}
```

```csharp
services.AddTelegramAuthentication<Guid, TelegramUserQueryService, RequestContext>()
```

Now the class ```RequestContext``` can be injected without defining request generic type in the each request.

```csharp
public class UserController : TelegramController
{
    [TelegramMessageRoute("/also-me")]
    public void PrintMyId(RequestContext requestContext)
    {
        Console.WriteLine(requestContext.UserId);
    }
```

The logic of retrieving user id field is next: from the received telegram message telegram identifier of the user that send the message takes.
Then ```ITelegramUserQueryService.FindUserIdAsync``` maps it to the system user identifier (```CreateAsync``` is called on the first
update from a new telegram user), and this identifier is set to the request context.

### Middleware
The package has the opportunity to extend request pipeline by adding custom middlewares. The example is the next
```csharp
public class LogExceptionsMiddleware : ITelegramMiddleware
{
    private readonly ITelegramMiddleware _next;
    private readonly TelegramRequestContext _telegramRequestContext;

    public LogExceptionsMiddleware(
        ITelegramMiddleware next,
        TelegramRequestContext telegramRequestContext)
    {
        _next = next;
        _telegramRequestContext = telegramRequestContext;
    }
    
    public async Task<object?> InvokeAsync(CancellationToken ct = default)
    {
        try
        {
            return await _next.InvokeAsync(ct);
        }
        catch (BadTelegramRequestException ex)
        {
            _logger.LogError(ex, "Error occured");
        }

        return null;
    }
}
```
Adding middleware to the request pipeline
```csharp
services.AddTelegramMiddleware<LogExceptionsMiddleware>();
```
**Note** - middlewares executes in the order they were added.

## Laraue.Telegram.NET.Interceptors

[![latest version](https://img.shields.io/nuget/v/Laraue.Telegram.NET.Interceptors)](https://www.nuget.org/packages/Laraue.Telegram.NET.Interceptors)
[![latest version](https://img.shields.io/nuget/dt/Laraue.Telegram.NET.Interceptors)](https://www.nuget.org/packages/Laraue.Telegram.NET.Interceptors)

The main case of the library is sometimes something from the user should be asked,
and next his answer should be considered as answer to this question.
This library allow to create such functionality. To use it should be implemented how to store Type that should be used to answer
for question.
```csharp
public class InterceptorState : BaseInterceptorState
{
    protected override Task<string?> TryGetStringIdentifierFromStorageAsync(string userId)
    {
        throw new NotImplementedException();
    }

    protected override Task SetStringIdentifierToStorageAsync(string userId, string id)
    {
        throw new NotImplementedException();
    }

    public override Task ResetAsync(string userId)
    {
        throw new NotImplementedException();
    }
}
```
Add the functionality to container. Interceptors are depending on _Laraue.Telegram.NET.Core_ 
and _Laraue.Telegram.NET.Authentication_ packages and should be registered in such order.
```csharp
services.AddTelegramCore(new TelegramBotClientOptions(builder.Configuration["Telegram:Token"]!))
    .AddTelegramRequestEfCoreInterceptors()
    .AddTelegramAuthentication<Guid, TelegramUserQueryService>()
```
**Note** - there is already implemented IInterceptorState with storing state in the DB via EFCore in the package
_Laraue.Telegram.NET.Interceptors.EFCore_.
```csharp
services.AddTelegramRequestEfCoreInterceptors<Guid, MyDbContext>(assemblies})
```

After interceptors state storage is setup, the interceptor can be implemented
```csharp
public class UpdateAgeInterceptor : BaseRequestInterceptor<Guid, uint, UpdateAgeContext>
{
    private readonly IUserRepository _repository;
    
    public UpdateAgeResponseInterceptor(IUserRepository repository)
    {
        _repository = repository;
    }
    
    protected override Task ValidateAsync(
        TelegramRequestContext<Guid> requestContext,
        InterceptResult<uint> interceptResult,
        UpdateAgeContext interceptorContext)
    {
        if (uint.TryParse(update.Message!.Text, out var age))
        {
            interceptResult.SetModel(age);
        }
        else
        {
            interceptResult.SetError("Age should be a positive number");
        }
        
        return Task.CompletedTask;
    }
    
    protected abstract Task ExecuteRouteAsync(
        TelegramRequestContext<Guid> requestContext,
        uint model,
        UpdateAgeContext interceptorContext)
    {
        return _repository.UpdateUserAgeAsync(interceptorContext.UserId, model);
    }
}

```
There is a short example of interceptor using
```csharp
public class TestController : TelegramController
{
    private readonly IInterceptorState _questionState;
    private readonly ITelegramBotClient _telegramClient;

    public TestController(IInterceptorState questionState, ITelegramBotClient telegramClient)
    {
        _questionStorage = questionState;
        _telegramClient = telegramClient;
    }

    [TelegramMessageRoute("/start")]
    public async Task HandleStart(TelegramRequestContext context)
    {
        await _client.SendTextMessageAsync(
            context.Update.GetUserId(),
            "What is your age?");
            
        await _questionState.SetAsync<UpdateAgeInterceptor, UpdateAgeInterceptorContext>(
            context.UserId,
            new UpdateAgeInterceptorContext
            {
                UserId = context.UserId
            });
    }
}
```

## Laraue.Telegram.NET.Testing
[![latest version](https://img.shields.io/nuget/v/Laraue.Telegram.NET.Testing)](https://www.nuget.org/packages/Laraue.Telegram.NET.Testing)
[![latest version](https://img.shields.io/nuget/dt/Laraue.Telegram.NET.Testing)](https://www.nuget.org/packages/Laraue.Telegram.NET.Testing)

The package allows to write integration tests for host built with Telegram.NET library.

### Usage
Create the class of Test Host implementing `TelegramTestHost` or `TelegramTestHost<TUserKey>` from the package.
```csharp
public class AppTelegramTestHost(IServiceCollection serviceCollection)
    : TelegramTestHost<Guid>(serviceCollection)
{
    protected override void BeforeFirstRequest()
    {
        // Do something before test server starts, migration run for example
        // When tests use real DB here can be it clearing
    }

    protected override void Dispose(bool disposing)
    {
        // Do something when the host is no more used
    }
}
```
My own recomendation for the next step is to create the base test class that have a method to run the host.
`XUnit` example:
```csharp
public class IntegrationTest
{
    protected static AppTelegramTestHost GetTelegramTestHost()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddJsonFile("appsettings.json");
        builder.AddApplicationServices();
        
        return new AppTelegramTestHost(appServices);
    }
}
```
Test infrastructure is ready. The test example:
```csharp
public class StartControllerTests : IntegrationTest
{
    [Fact]
    public async Task Start_ShouldSendWelcome_Always()
    {
        // Run the host from the base class
        using var telegramTestHost = GetTelegramTestHost();
    
        // Emulate new message in chat
        await telegramTestHost.SendUpdateAsync(new Update
        {
            Message = new Message
            {
                Text = "/start",
                From = DefaultUser, // Can be determined in base class to avoid repeating
            }
        });
        
        // Ensure only one message was sent back to Telegram
        var request = telegramTestHost
            .Requests()
            .Single<EditMessageTextRequest>();
   
        // Check the message text 
        request.CheckMessage("Hello, user");
        
        // Check the buttons in the message
        request.CheckButtonsSequentially(buttons => 
            buttons
                .HasButtonsRow(
                    new ButtonAssert("Menu", "menu")));
    }
}
```
More complex asserts can be made with the services requested from the Test Host container:
```csharp
using var telegramTestHost = GetTelegramTestHost();
using var scope = telegramTestHost.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

Assert.Single(dbContext.Users);
```