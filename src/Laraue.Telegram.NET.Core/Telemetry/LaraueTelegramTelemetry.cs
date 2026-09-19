using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Laraue.Telegram.NET.Core.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> and <see cref="Meter"/> used to report telemetry for every
/// Telegram update processed by <see cref="Routing.TelegramRouter"/>.
/// Consuming apps opt in by name, no OpenTelemetry package reference is required here:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(LaraueTelegramTelemetry.SourceName))
///     .WithMetrics(m => m.AddMeter(LaraueTelegramTelemetry.SourceName));
/// </code>
/// </summary>
public static class LaraueTelegramTelemetry
{
    public const string SourceName = "Laraue.Telegram.NET";

    private static readonly string Version =
        typeof(LaraueTelegramTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    private static readonly Meter Meter = new(SourceName, Version);

    /// <summary>
    /// Duration of processing a single incoming Telegram update, in milliseconds.
    /// Tagged with <see cref="TelegramTags.UpdateType"/>, <see cref="TelegramTags.RouteName"/>
    /// (when a route was matched) and <see cref="TelegramTags.Status"/>.
    /// </summary>
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "telegram.request.duration",
        unit: "ms",
        description: "Duration of processing an incoming Telegram update.");

    /// <summary>
    /// Number of Telegram updates that started processing, tagged with <see cref="TelegramTags.UpdateType"/>.
    /// </summary>
    public static readonly Counter<long> RequestsStarted = Meter.CreateCounter<long>(
        "telegram.requests.started",
        description: "Number of Telegram updates started processing.");

    /// <summary>
    /// Number of Telegram updates that failed to process, tagged with <see cref="TelegramTags.Status"/>
    /// (<c>not_found</c> when no route matched, <c>error</c> when a handler threw).
    /// </summary>
    public static readonly Counter<long> RequestsFailed = Meter.CreateCounter<long>(
        "telegram.requests.failed",
        description: "Number of Telegram updates that failed to process.");
}
