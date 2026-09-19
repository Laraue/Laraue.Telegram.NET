using Laraue.Telegram.NET.Core.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Laraue.Telegram.NET.OpenTelemetry;

/// <summary>
/// Subscribes the <c>"Laraue.Telegram.NET"</c> <see cref="System.Diagnostics.ActivitySource"/>/
/// <see cref="System.Diagnostics.Metrics.Meter"/> by name, so callers don't need to know or
/// hard-code that string themselves.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
    public static TracerProviderBuilder AddLaraueTelegram(this TracerProviderBuilder builder) =>
        builder.AddSource(LaraueTelegramTelemetry.SourceName);

    public static MeterProviderBuilder AddLaraueTelegram(this MeterProviderBuilder builder) =>
        builder.AddMeter(LaraueTelegramTelemetry.SourceName);
}
