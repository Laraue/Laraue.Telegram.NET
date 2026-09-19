using System.Diagnostics;
using Telegram.Bot.Types;

namespace Laraue.Telegram.NET.Core.Telemetry;

/// <summary>
/// Tracks a single Telegram update from start to completion: opens an <see cref="Activity"/> and
/// records <see cref="LaraueTelegramTelemetry"/> duration/failure metrics on
/// <see cref="Complete(string?)"/>/<see cref="Complete(Exception)"/>. Used by
/// <see cref="Routing.TelegramRouter"/> so every update processed through the pipeline is measured
/// identically.
/// </summary>
public readonly struct RequestScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _updateType;
    private readonly long _startTimestamp;

    private RequestScope(Activity? activity, string updateType, long startTimestamp)
    {
        _activity = activity;
        _updateType = updateType;
        _startTimestamp = startTimestamp;
    }

    public static RequestScope Start(Update update)
    {
        var updateType = update.Type.ToString();

        var activity = LaraueTelegramTelemetry.ActivitySource.StartActivity(
            "Telegram.Update.Process",
            ActivityKind.Server);

        activity?.SetTag(TelegramTags.UpdateType, updateType);

        LaraueTelegramTelemetry.RequestsStarted.Add(
            1,
            new KeyValuePair<string, object?>(TelegramTags.UpdateType, updateType));

        return new RequestScope(activity, updateType, Stopwatch.GetTimestamp());
    }

    /// <summary>Marks the update as successfully processed by <paramref name="routeName"/>.</summary>
    public void Complete(string? routeName)
    {
        _activity?.SetTag(TelegramTags.RouteName, routeName);
        _activity?.SetTag(TelegramTags.Status, "success");

        Record(routeName, "success");
    }

    /// <summary>Marks the update as failed. <see cref="RouteNotFoundException"/> is reported with the
    /// <c>not_found</c> status, any other exception with <c>error</c>.</summary>
    public void Complete(Exception exception)
    {
        var status = exception is Routing.RouteNotFoundException
            ? "not_found"
            : "error";

        _activity?.SetTag(TelegramTags.Status, status);
        _activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        LaraueTelegramTelemetry.RequestsFailed.Add(
            1,
            BaseTags(routeName: null, status));

        Record(routeName: null, status);
    }

    private void Record(string? routeName, string status)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;

        LaraueTelegramTelemetry.RequestDuration.Record(elapsedMs, BaseTags(routeName, status));
    }

    private TagList BaseTags(string? routeName, string status)
    {
        var tags = new TagList
        {
            { TelegramTags.UpdateType, _updateType },
            { TelegramTags.Status, status },
        };

        if (routeName is not null)
        {
            tags.Add(TelegramTags.RouteName, routeName);
        }

        return tags;
    }

    public void Dispose() => _activity?.Dispose();
}
