using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Laraue.Telegram.NET.OpenTelemetry;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// One-liner for wiring up Laraue.Telegram.NET's traces and metrics into OpenTelemetry, instead of
    /// every service having to remember to <c>AddSource("Laraue.Telegram.NET")</c> /
    /// <c>AddMeter("Laraue.Telegram.NET")</c> by hand. Exporters (OTLP, Console, Prometheus, ...) are still
    /// the caller's choice via <paramref name="configureTracing"/>/<paramref name="configureMetrics"/> -
    /// this only wires up what's specific to Laraue.Telegram.NET, it doesn't pick exporters for you.
    /// </summary>
    public static IOpenTelemetryBuilder AddLaraueTelegramTelemetry(
        this IServiceCollection services,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        return services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddLaraueTelegram();
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddLaraueTelegram();
                configureMetrics?.Invoke(metrics);
            });
    }
}
